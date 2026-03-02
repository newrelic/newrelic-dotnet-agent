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

    public bool IsTransactionRequired => false;
    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var builder = instrumentedMethodCall.MethodCall.InvocationTarget;

        Log.Finest($"KafkaBuilderWrapper: BeforeWrappedMethod called for builder type: {builder?.GetType().Name}");

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

        Log.Finest($"KafkaBuilderWrapper: Found bootstrap servers: {bootstrapServers ?? "null"}");

        // Set up statistics collection BEFORE Build() is called
        SetupStatisticsCollection(builder, agent);

        return Delegates.GetDelegateFor<object>(onSuccess: (clientAsObject) => {
            Log.Debug($"KafkaBuilderWrapper: Build completed, client type: {clientAsObject?.GetType().Name}");

            // Store bootstrap servers for node metrics
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
        Log.Finest($"KafkaBuilderWrapper: SetupStatisticsCollection called for builder type: {builder?.GetType().Name}");

        // Create and set our statistics handler
        var ourHandler = CreateMetricsReportingHandler(agent, builder);
        if (ourHandler != null)
        {
            Log.Finest("KafkaBuilderWrapper: Setting up statistics handler");
            SetStatisticsHandlerOnBuilder(builder, ourHandler);

            // For testing, set a statistics interval (normally customer should set this)
            SetStatisticsIntervalOnBuilder(builder, 5000); // 5 seconds for testing

            Log.Debug("KafkaBuilderWrapper: Statistics handler configured successfully");
        }
        else
        {
            Log.Debug("KafkaBuilderWrapper: Could not create statistics handler");
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

    /// <summary>
    /// The actual method that will be called by the statistics callback.
    /// This method signature works with both producer and consumer delegates.
    /// </summary>
    private void StatisticsHandlerMethod(object client, string json)
    {
        var clientTypeName = client?.GetType().Name ?? "Unknown";
        Log.Debug($"KafkaBuilderWrapper: Statistics callback triggered for {clientTypeName}, JSON length: {json?.Length ?? 0}");

        if (_currentAgent != null)
        {
            ParseAndReportCriticalUIMetrics(_currentAgent, json);
        }
        else
        {
            Log.Finest("KafkaBuilderWrapper: No agent available for statistics processing");
        }
    }

    /// <summary>
    /// Parses statistics JSON and reports the critical metrics that light up New Relic's Kafka UI.
    /// Uses KafkaStatisticsHelper from Extensions project to handle JSON parsing.
    /// </summary>
    private void ParseAndReportCriticalUIMetrics(IAgent agent, string statisticsJson)
    {
        Log.Finest($"KafkaBuilderWrapper: ParseAndReportCriticalUIMetrics called with JSON length: {statisticsJson?.Length ?? 0}");

        if (string.IsNullOrEmpty(statisticsJson))
        {
            Log.Debug("KafkaBuilderWrapper: Statistics JSON is null or empty");
            return;
        }

        // Use the helper from Extensions project to parse the JSON
        var metricsData = KafkaStatisticsHelper.ParseStatistics(statisticsJson);
        if (metricsData?.IsValid != true)
        {
            Log.Debug("KafkaBuilderWrapper: Failed to parse Kafka statistics or data is invalid");
            return;
        }

        Log.Debug($"KafkaBuilderWrapper: Parsed stats - ClientId: {metricsData.ClientId}, Type: {metricsData.ClientType}, Requests: {metricsData.RequestCount}, Responses: {metricsData.ResponseCount}");

        // Get all metrics as a dictionary
        var metricsDict = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, MessageBrokerVendorConstants.Kafka);

        // Record all metrics using the experimental API
        foreach (var kvp in metricsDict)
        {
            agent.GetExperimentalApi().RecordCountMetric(kvp.Key, kvp.Value);
            Log.Finest($"KafkaBuilderWrapper: Recorded metric: {kvp.Key} = {kvp.Value}");
        }

        Log.Finest($"KafkaBuilderWrapper: Successfully recorded {metricsDict.Count} Kafka internal metrics");
    }


    /// <summary>
    /// Sets statistics handler on the Kafka builder using reflection.
    /// Handles both properly typed and generic handlers.
    /// </summary>
    private void SetStatisticsHandlerOnBuilder(object builder, object handler)
    {
        Log.Finest($"KafkaBuilderWrapper: SetStatisticsHandlerOnBuilder called for type: {builder.GetType().Name}");

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

    /// <summary>
    /// Attempts to enable statistics on the Kafka builder.
    /// Note: Statistics are typically configured by the customer application.
    /// </summary>
    private void SetStatisticsIntervalOnBuilder(object builder, int intervalMs)
    {
        // Try to enable statistics via SetConfig method
        var setConfigMethod = builder.GetType().GetMethod("SetConfig", new[] { typeof(string), typeof(string) });
        if (setConfigMethod != null)
        {
            setConfigMethod.Invoke(builder, new object[] { "statistics.interval.ms", intervalMs.ToString() });
            Log.Finest($"KafkaBuilderWrapper: Set statistics interval to {intervalMs}ms");
        }
        else
        {
            Log.Finest("KafkaBuilderWrapper: No SetConfig method found on builder");
        }
    }







    #endregion
}
