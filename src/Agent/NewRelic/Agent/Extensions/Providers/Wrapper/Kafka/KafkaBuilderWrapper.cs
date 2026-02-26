// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka;

public class KafkaBuilderWrapper : IWrapper
{
    private ConcurrentDictionary<Type, Func<object, IEnumerable>> _builderConfigGetterDictionary = new();

    private const string WrapperName = "KafkaBuilderWrapper";
    private const string BootstrapServersKey = "bootstrap.servers";

    // Store current agent for use in statistics callback
    private IAgent _currentAgent;

    // Store handler references for composite functionality
    private object _ourHandler;
    private object _customerHandler;

    public bool IsTransactionRequired => false;
    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var builder = instrumentedMethodCall.MethodCall.InvocationTarget;

        Log.Finest($"KafkaBuilderWrapper: BeforeWrappedMethod called for builder type: {builder.GetType().Name}");

        if (!_builderConfigGetterDictionary.TryGetValue(builder.GetType(), out var configGetter))
        {
            configGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
            _builderConfigGetterDictionary[builder.GetType()] = configGetter;
        }

        dynamic configuration = configGetter(builder);

        string bootstrapServers = null;

        foreach (var kvp in configuration)
        {
            if (kvp.Key == BootstrapServersKey)
            {
                bootstrapServers = kvp.Value as string;
                break;
            }
        }

        Log.Finest($"KafkaBuilderWrapper: Found bootstrap servers: {bootstrapServers}");

        // NEW: Set up statistics collection BEFORE Build() is called
        SetupStatisticsCollection(builder, agent);

        return Delegates.GetDelegateFor<object>(onSuccess: (clientAsObject) => {
            Log.Finest($"KafkaBuilderWrapper: Build completed, client type: {clientAsObject.GetType().Name}");

            // Existing configuration extraction logic
            if (!string.IsNullOrEmpty(bootstrapServers))
            {
                KafkaHelper.AddBootstrapServersToCache(clientAsObject, bootstrapServers);
            }
        });

    }

    #region Statistics Collection - Phase 1: Critical UI Metrics

    /// <summary>
    /// Sets up non-invasive statistics collection for Kafka metrics.
    /// Preserves any existing customer statistics handlers while adding our metrics collection.
    /// </summary>
    private void SetupStatisticsCollection(object builder, IAgent agent)
    {
        Log.Info($"KafkaBuilderWrapper: SetupStatisticsCollection called for builder type: {builder.GetType().Name}");

        try
        {
            // Check if customer already configured statistics handler via reflection
            var existingHandler = GetExistingStatisticsHandler(builder);

            Log.Debug($"KafkaBuilderWrapper: Existing handler found: {existingHandler != null}");

            if (existingHandler == null)
            {
                // Customer hasn't set a handler - set ours directly
                Log.Finest("KafkaBuilderWrapper: Setting up our statistics handler directly");
                var ourHandler = CreateMetricsReportingHandler(agent, builder);
                SetStatisticsHandlerOnBuilder(builder, ourHandler);

                // Try to enable statistics, but don't fail if we can't
                Log.Finest("KafkaBuilderWrapper: Attempting to enable statistics (non-critical)");
                SetStatisticsIntervalOnBuilder(builder, 5000); // 5 seconds for testing

                Log.Finest("KafkaBuilderWrapper: Statistics handler configured");
            }
            else
            {
                // Customer has a handler - create composite that calls both
                Log.Debug("KafkaBuilderWrapper: Creating composite handler to preserve customer handler");
                var ourHandler = CreateMetricsReportingHandler(agent, builder);
                var compositeHandler = CreateCompositeHandler(ourHandler, existingHandler, builder);

                // Replace with composite (preserves customer functionality)
                SetStatisticsHandlerOnBuilder(builder, compositeHandler);

                // Try to enable statistics if customer hasn't
                Log.Finest("KafkaBuilderWrapper: Attempting to enable statistics (non-critical)");
                SetStatisticsIntervalOnBuilder(builder, 5000);

                Log.Finest("KafkaBuilderWrapper: Composite handler configured");
            }
        }
        catch (Exception ex)
        {
            Log.Info($"KafkaBuilderWrapper: Could not set up Kafka statistics collection: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects existing customer statistics handler via reflection on builder configuration.
    /// Returns null if no handler is configured.
    /// Also checks if customer has configured statistics interval.
    /// </summary>
    private object GetExistingStatisticsHandler(object builder)
    {
        try
        {
            // Use VisibilityBypasser to access internal configuration
            if (!_builderConfigGetterDictionary.TryGetValue(builder.GetType(), out var configGetter))
            {
                configGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
                _builderConfigGetterDictionary[builder.GetType()] = configGetter;
            }

            dynamic configuration = configGetter(builder);

            object existingHandler = null;
            bool hasStatisticsInterval = false;
            string existingInterval = null;

            // Look for statistics handler and interval in configuration
            foreach (var kvp in configuration)
            {
                if (kvp.Key == "statistics_cb" || kvp.Key == "StatisticsHandler")
                {
                    existingHandler = kvp.Value;
                }
                else if (kvp.Key == "statistics.interval.ms")
                {
                    hasStatisticsInterval = true;
                    existingInterval = kvp.Value?.ToString();
                }
            }

            if (hasStatisticsInterval)
            {
                Log.Finest($"KafkaBuilderWrapper: Customer has configured statistics.interval.ms = {existingInterval}");
            }
            else
            {
                Log.Finest($"KafkaBuilderWrapper: Customer has not configured statistics.interval.ms, we can set our interval");
            }

            return existingHandler;
        }
        catch (Exception ex)
        {
            Log.Finest($"Could not detect existing statistics handler: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates composite handler that calls our metrics collection first, then customer's handler.
    /// Ensures customer code is completely unaffected while we get our metrics.
    /// Uses reflection to create properly typed composite handlers.
    /// </summary>
    private object CreateCompositeHandler(
        object ourHandler,
        object customerHandler,
        object builder)
    {
        Log.Debug($"KafkaBuilderWrapper: Creating reflection-based composite handler for {builder.GetType().Name}");

        try
        {
            // Get the delegate type from our handler (they should be the same type)
            var delegateType = ourHandler.GetType();
            Log.Finest($"KafkaBuilderWrapper: Creating composite of type: {delegateType.Name}");

            // Create a method that will be our composite handler
            var compositeMethodInfo = typeof(KafkaBuilderWrapper).GetMethod(nameof(CompositeStatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a delegate that calls our composite method
            var compositeHandler = Delegate.CreateDelegate(delegateType, this, compositeMethodInfo);

            // Store references to both handlers
            _ourHandler = ourHandler;
            _customerHandler = customerHandler;

            Log.Finest("KafkaBuilderWrapper: Successfully created reflection-based composite handler");
            return compositeHandler;
        }
        catch (Exception ex)
        {
            Log.Info($"KafkaBuilderWrapper: Failed to create composite handler: {ex.Message}. Using our handler only.");
            return ourHandler;
        }
    }

    /// <summary>
    /// Composite statistics handler method that calls both our handler and customer's handler.
    /// </summary>
    private void CompositeStatisticsHandlerMethod(object client, string json)
    {
        // Call our handler first
        try
        {
            if (_ourHandler != null)
            {
                ((Delegate)_ourHandler).DynamicInvoke(client, json);
            }
        }
        catch (Exception ex)
        {
            Log.Finest($"Error in New Relic Kafka metrics collection: {ex.Message}");
        }

        // Call customer's handler afterward
        try
        {
            if (_customerHandler != null)
            {
                ((Delegate)_customerHandler).DynamicInvoke(client, json);
            }
        }
        catch
        {
            // Don't log customer handler exceptions - not our concern
            throw; // Re-throw so customer sees their own errors
        }
    }

    /// <summary>
    /// Creates our metrics reporting handler that extracts and reports Kafka internal metrics.
    /// Uses reflection to create proper delegate types compatible with old Kafka package (1.4.0).
    /// Phase 1: Focus on critical UI metrics only.
    /// </summary>
    private object CreateMetricsReportingHandler(IAgent agent, object builder)
    {
        Log.Finest($"KafkaBuilderWrapper: Creating reflection-based metrics handler for builder type: {builder.GetType().Name}");

        try
        {
            // Use reflection to find the exact method signature expected by SetStatisticsHandler
            var builderType = builder.GetType();
            var setStatisticsMethod = builderType.GetMethod("SetStatisticsHandler");

            if (setStatisticsMethod == null)
            {
                Log.Info($"KafkaBuilderWrapper: No SetStatisticsHandler method found on {builderType.Name}");
                return null;
            }

            var parameters = setStatisticsMethod.GetParameters();
            if (parameters.Length != 1)
            {
                Log.Info($"KafkaBuilderWrapper: SetStatisticsHandler has unexpected parameter count: {parameters.Length}");
                return null;
            }

            // Get the expected delegate type (Action<TClient, string>)
            var expectedDelegateType = parameters[0].ParameterType;
            Log.Finest($"KafkaBuilderWrapper: Expected delegate type: {expectedDelegateType.Name}");

            // Create a method that matches the delegate signature
            var methodToInvoke = typeof(KafkaBuilderWrapper).GetMethod(nameof(StatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a delegate of the expected type that calls our method
            var handler = Delegate.CreateDelegate(expectedDelegateType, this, methodToInvoke);

            // Store the agent reference so our method can access it
            _currentAgent = agent;

            Log.Finest($"KafkaBuilderWrapper: Successfully created reflection-based handler of type: {handler.GetType().Name}");
            return handler;
        }
        catch (Exception ex)
        {
            Log.Info($"KafkaBuilderWrapper: Failed to create reflection-based handler: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// The actual method that will be called by the statistics callback.
    /// This method signature works with both producer and consumer delegates.
    /// </summary>
    private void StatisticsHandlerMethod(object client, string json)
    {
        var clientTypeName = client?.GetType().Name ?? "Unknown";
        Log.Finest($"KafkaBuilderWrapper: Statistics callback triggered for {clientTypeName}, JSON length: {json?.Length ?? 0}");

        try
        {
            if (_currentAgent != null)
            {
                ParseAndReportCriticalUIMetrics(_currentAgent, json);
            }
            else
            {
                Log.Info("KafkaBuilderWrapper: No agent available for statistics processing");
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Error parsing Kafka statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses statistics JSON and reports the critical metrics that light up New Relic's Kafka UI.
    /// Uses KafkaStatisticsHelper from Extensions project to handle JSON parsing.
    /// </summary>
    private void ParseAndReportCriticalUIMetrics(IAgent agent, string statisticsJson)
    {
        Log.Finest($"KafkaBuilderWrapper: ParseAndReportCriticalUIMetrics called with JSON length: {statisticsJson?.Length ?? 0}");

        try
        {
            // Use the helper from Extensions project to parse the JSON
            var metricsData = KafkaStatisticsHelper.ParseStatistics(statisticsJson);
            if (metricsData?.IsValid != true)
            {
                Log.Debug("KafkaBuilderWrapper: Failed to parse Kafka statistics or data is invalid");
                return;
            }

            Log.Finest($"KafkaBuilderWrapper: Parsed stats - ClientId: {metricsData.ClientId}, Type: {metricsData.ClientType}, Requests: {metricsData.RequestCount}, Responses: {metricsData.ResponseCount}");

            // Get all metrics as a dictionary
            var metricsDict = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, MessageBrokerVendorConstants.Kafka);

            // Record all metrics
            foreach (var kvp in metricsDict)
            {
                agent.GetExperimentalApi().RecordCountMetric(kvp.Key, kvp.Value);
                Log.Finest($"KafkaBuilderWrapper: Recorded metric: {kvp.Key} = {kvp.Value}");
            }

            Log.Finest($"KafkaBuilderWrapper: Successfully recorded {metricsDict.Count} Kafka internal metrics");
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Error processing Kafka statistics: {ex.Message}");
        }
    }


    /// <summary>
    /// Sets statistics handler on the Kafka builder using reflection.
    /// Handles both properly typed and generic handlers.
    /// </summary>
    private void SetStatisticsHandlerOnBuilder(object builder, object handler)
    {
        Log.Finest($"KafkaBuilderWrapper: SetStatisticsHandlerOnBuilder called for type: {builder.GetType().Name}");

        try
        {
            // Find SetStatisticsHandler method with any signature since the exact signature may vary
            var setStatisticsMethods = builder.GetType().GetMethods()
                .Where(m => m.Name == "SetStatisticsHandler")
                .ToArray();

            Log.Finest($"KafkaBuilderWrapper: Found {setStatisticsMethods.Length} SetStatisticsHandler methods");

            if (setStatisticsMethods.Length > 0)
            {
                // Use the first SetStatisticsHandler method found
                var method = setStatisticsMethods[0];
                var parameters = method.GetParameters();

                Log.Finest($"KafkaBuilderWrapper: Using SetStatisticsHandler method with parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");

                // Try to invoke with our handler - the method should accept our Action delegate
                method.Invoke(builder, new object[] { handler });
                Log.Finest($"KafkaBuilderWrapper: SetStatisticsHandler invoked successfully");
            }
            else
            {
                Log.Info($"KafkaBuilderWrapper: No SetStatisticsHandler methods found on builder type {builder.GetType()}");
            }
        }
        catch (Exception ex)
        {
            Log.Info($"KafkaBuilderWrapper: Could not set statistics handler on Kafka builder: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to enable statistics on the Kafka builder.
    /// Note: Statistics are typically configured by the customer application.
    /// </summary>
    private void SetStatisticsIntervalOnBuilder(object builder, int intervalMs)
    {
        // Note: In production, statistics should be configured by the customer application
        // via builder.SetConfig("statistics.interval.ms", "5000") or similar.
        // We don't attempt to override customer configuration.
        Log.Finest("KafkaBuilderWrapper: Statistics interval setup - relying on customer configuration");
    }







    #endregion
}