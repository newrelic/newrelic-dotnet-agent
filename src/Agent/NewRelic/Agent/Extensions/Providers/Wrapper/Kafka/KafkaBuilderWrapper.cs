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

        Log.Info($"KafkaBuilderWrapper: BeforeWrappedMethod called for builder type: {builder.GetType().Name}");
        Log.Debug($"KafkaBuilderWrapper: BeforeWrappedMethod called for builder type: {builder.GetType().Name}");

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

        Log.Debug($"KafkaBuilderWrapper: Found bootstrap servers: {bootstrapServers}");

        // NEW: Set up statistics collection BEFORE Build() is called
        SetupStatisticsCollection(builder, agent);

        return Delegates.GetDelegateFor<object>(onSuccess: (clientAsObject) => {
            Log.Debug($"KafkaBuilderWrapper: Build completed, client type: {clientAsObject.GetType().Name}");

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
        Log.Debug($"KafkaBuilderWrapper: SetupStatisticsCollection called for builder type: {builder.GetType().Name}");

        try
        {
            // Check if customer already configured statistics handler via reflection
            var existingHandler = GetExistingStatisticsHandler(builder);

            Log.Debug($"KafkaBuilderWrapper: Existing handler found: {existingHandler != null}");

            if (existingHandler == null)
            {
                // Customer hasn't set a handler - set ours directly
                Log.Debug("KafkaBuilderWrapper: Setting up our statistics handler directly");
                var ourHandler = CreateMetricsReportingHandler(agent, builder);
                SetStatisticsHandlerOnBuilder(builder, ourHandler);

                // Try to enable statistics, but don't fail if we can't
                Log.Debug("KafkaBuilderWrapper: Attempting to enable statistics (non-critical)");
                SetStatisticsIntervalOnBuilder(builder, 5000); // 5 seconds for testing

                Log.Debug("KafkaBuilderWrapper: Statistics handler configured - testing if callback works with existing config");
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
                Log.Debug("KafkaBuilderWrapper: Attempting to enable statistics (non-critical)");
                SetStatisticsIntervalOnBuilder(builder, 5000);

                Log.Debug("KafkaBuilderWrapper: Composite handler configured");
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
                Log.Debug($"KafkaBuilderWrapper: Customer has configured statistics.interval.ms = {existingInterval}");
            }
            else
            {
                Log.Debug($"KafkaBuilderWrapper: Customer has not configured statistics.interval.ms, we can set our interval");
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
            Log.Debug($"KafkaBuilderWrapper: Creating composite of type: {delegateType.Name}");

            // Create a method that will be our composite handler
            var compositeMethodInfo = typeof(KafkaBuilderWrapper).GetMethod(nameof(CompositeStatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a delegate that calls our composite method
            var compositeHandler = Delegate.CreateDelegate(delegateType, this, compositeMethodInfo);

            // Store references to both handlers
            _ourHandler = ourHandler;
            _customerHandler = customerHandler;

            Log.Debug("KafkaBuilderWrapper: Successfully created reflection-based composite handler");
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
        Log.Debug($"KafkaBuilderWrapper: Creating reflection-based metrics handler for builder type: {builder.GetType().Name}");

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
            Log.Debug($"KafkaBuilderWrapper: Expected delegate type: {expectedDelegateType.Name}");

            // Create a method that matches the delegate signature
            var methodToInvoke = typeof(KafkaBuilderWrapper).GetMethod(nameof(StatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);

            // Create a delegate of the expected type that calls our method
            var handler = Delegate.CreateDelegate(expectedDelegateType, this, methodToInvoke);

            // Store the agent reference so our method can access it
            _currentAgent = agent;

            Log.Debug($"KafkaBuilderWrapper: Successfully created reflection-based handler of type: {handler.GetType().Name}");
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
        Log.Debug($"KafkaBuilderWrapper: Statistics callback triggered for {clientTypeName}, JSON length: {json?.Length ?? 0}");

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
            Log.Info($"KafkaBuilderWrapper: Error parsing Kafka statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses statistics JSON and reports the critical metrics that light up New Relic's Kafka UI.
    /// Uses KafkaStatisticsHelper from Extensions project to handle JSON parsing.
    /// </summary>
    private void ParseAndReportCriticalUIMetrics(IAgent agent, string statisticsJson)
    {
        Log.Debug($"KafkaBuilderWrapper: ParseAndReportCriticalUIMetrics called with JSON length: {statisticsJson?.Length ?? 0}");

        try
        {
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

            // Record all metrics
            foreach (var kvp in metricsDict)
            {
                agent.GetExperimentalApi().RecordCountMetric(kvp.Key, kvp.Value);
                Log.Debug($"KafkaBuilderWrapper: Recorded metric: {kvp.Key} = {kvp.Value}");
            }

            Log.Debug($"KafkaBuilderWrapper: Successfully recorded {metricsDict.Count} Kafka internal metrics");
        }
        catch (Exception ex)
        {
            Log.Info($"KafkaBuilderWrapper: Error processing Kafka statistics: {ex.Message}");
        }
    }


    /// <summary>
    /// Sets statistics handler on the Kafka builder using reflection.
    /// Handles both properly typed and generic handlers.
    /// </summary>
    private void SetStatisticsHandlerOnBuilder(object builder, object handler)
    {
        Log.Debug($"KafkaBuilderWrapper: SetStatisticsHandlerOnBuilder called for type: {builder.GetType().Name}");

        try
        {
            // Find SetStatisticsHandler method with any signature since the exact signature may vary
            var setStatisticsMethods = builder.GetType().GetMethods()
                .Where(m => m.Name == "SetStatisticsHandler")
                .ToArray();

            Log.Debug($"KafkaBuilderWrapper: Found {setStatisticsMethods.Length} SetStatisticsHandler methods");

            if (setStatisticsMethods.Length > 0)
            {
                // Use the first SetStatisticsHandler method found
                var method = setStatisticsMethods[0];
                var parameters = method.GetParameters();

                Log.Debug($"KafkaBuilderWrapper: Using SetStatisticsHandler method with parameters: {string.Join(", ", parameters.Select(p => p.ParameterType.Name))}");

                // Try to invoke with our handler - the method should accept our Action delegate
                method.Invoke(builder, new object[] { handler });
                Log.Debug($"KafkaBuilderWrapper: SetStatisticsHandler invoked successfully");
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
    /// Enables statistics on the Kafka builder using various approaches.
    /// </summary>
    private void SetStatisticsIntervalOnBuilder(object builder, int intervalMs)
    {
        Log.Debug($"KafkaBuilderWrapper: SetStatisticsIntervalOnBuilder called for interval: {intervalMs}ms");

        try
        {
            var builderType = builder.GetType();
            Log.Debug($"KafkaBuilderWrapper: Builder type: {builderType.Name}");

            // Approach 1: Try SetConfig method (some builders might have this)
            if (TrySetConfigMethod(builder, intervalMs))
                return;

            // Approach 2: Try Set method with string parameters
            if (TrySetMethod(builder, intervalMs))
                return;

            // Approach 3: Try accessing internal Config property/field
            if (TryConfigPropertyAccess(builder, intervalMs))
                return;

            // Approach 4: Try any method that contains "Config" or "Set"
            if (TryReflectionConfigMethods(builder, intervalMs))
                return;

            Log.Debug($"KafkaBuilderWrapper: All statistics configuration approaches failed");
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Error in statistics configuration: {ex.Message}");
        }
    }

    private bool TrySetConfigMethod(object builder, int intervalMs)
    {
        try
        {
            var setConfigMethod = builder.GetType().GetMethod("SetConfig");
            if (setConfigMethod != null)
            {
                // Check if we can read existing config first (some builders might have GetConfig)
                var getConfigMethod = builder.GetType().GetMethod("GetConfig");
                if (getConfigMethod != null)
                {
                    try
                    {
                        var existingValue = getConfigMethod.Invoke(builder, new object[] { "statistics.interval.ms" });
                        if (existingValue != null && !string.IsNullOrEmpty(existingValue.ToString()))
                        {
                            Log.Debug($"KafkaBuilderWrapper: Customer has configured statistics.interval.ms = {existingValue}, respecting their setting");
                            return true; // Don't override
                        }
                    }
                    catch
                    {
                        // GetConfig failed, proceed with setting our value
                    }
                }

                Log.Debug("KafkaBuilderWrapper: Trying SetConfig method");
                setConfigMethod.Invoke(builder, new object[] { "statistics.interval.ms", intervalMs.ToString() });
                Log.Debug($"KafkaBuilderWrapper: SetConfig succeeded with interval {intervalMs}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: SetConfig failed: {ex.Message}");
        }
        return false;
    }

    private bool TrySetMethod(object builder, int intervalMs)
    {
        try
        {
            var setMethods = builder.GetType().GetMethods()
                .Where(m => m.Name == "Set" && m.GetParameters().Length == 2)
                .ToArray();

            foreach (var method in setMethods)
            {
                var parameters = method.GetParameters();
                if (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(string))
                {
                    // Note: We can't easily check existing config with Set method since it's write-only
                    // But this is less invasive than SetConfig since it's typically additive
                    Log.Debug("KafkaBuilderWrapper: Trying Set(string, string) method");
                    method.Invoke(builder, new object[] { "statistics.interval.ms", intervalMs.ToString() });
                    Log.Debug($"KafkaBuilderWrapper: Set method succeeded with interval {intervalMs}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Set method failed: {ex.Message}");
        }
        return false;
    }

    private bool TryConfigPropertyAccess(object builder, int intervalMs)
    {
        try
        {
            var builderType = builder.GetType();
            Log.Debug($"KafkaBuilderWrapper: Examining builder type: {builderType.FullName}");

            // List all properties and fields for debugging
            var properties = builderType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fields = builderType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Log.Debug($"KafkaBuilderWrapper: Available properties: {string.Join(", ", properties.Select(p => p.Name))}");
            Log.Debug($"KafkaBuilderWrapper: Available fields: {string.Join(", ", fields.Select(f => f.Name))}");

            // Try all possible config-related properties/fields
            foreach (var prop in properties)
            {
                if (prop.Name.ToLower().Contains("config"))
                {
                    try
                    {
                        var config = prop.GetValue(builder);
                        if (config != null)
                        {
                            Log.Debug($"KafkaBuilderWrapper: Found config property '{prop.Name}', type: {config.GetType().Name}");

                            if (TryDictionaryAccess(config, intervalMs))
                                return true;

                            if (TryAddMethod(config, intervalMs))
                                return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"KafkaBuilderWrapper: Failed to access property {prop.Name}: {ex.Message}");
                    }
                }
            }

            foreach (var field in fields)
            {
                if (field.Name.ToLower().Contains("config"))
                {
                    try
                    {
                        var config = field.GetValue(builder);
                        if (config != null)
                        {
                            Log.Debug($"KafkaBuilderWrapper: Found config field '{field.Name}', type: {config.GetType().Name}");

                            if (TryDictionaryAccess(config, intervalMs))
                                return true;

                            if (TryAddMethod(config, intervalMs))
                                return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"KafkaBuilderWrapper: Failed to access field {field.Name}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Config property access failed: {ex.Message}");
        }
        return false;
    }

    private bool TryDictionaryAccess(object config, int intervalMs)
    {
        try
        {
            Log.Debug($"KafkaBuilderWrapper: Trying to access config of type: {config.GetType().Name}");

            // The config is IEnumerable<KeyValuePair<string, string>>, not a dictionary
            // We need to enumerate it to check existing values and potentially modify it
            var configEnumerable = config as IEnumerable;
            if (configEnumerable != null)
            {
                bool hasStatisticsInterval = false;
                string existingValue = null;

                // Check if statistics.interval.ms is already configured
                foreach (var item in configEnumerable)
                {
                    var kvp = item as dynamic;
                    if (kvp != null && kvp.Key == "statistics.interval.ms")
                    {
                        hasStatisticsInterval = true;
                        existingValue = kvp.Value?.ToString();
                        break;
                    }
                }

                if (hasStatisticsInterval)
                {
                    Log.Debug($"KafkaBuilderWrapper: Customer has configured statistics.interval.ms = {existingValue}, respecting their setting");
                    return true; // Customer has it configured, respect their setting
                }

                // Customer hasn't configured it - we need to add it
                // But we can't modify an enumerable directly
                // This approach won't work for IEnumerable
                Log.Debug($"KafkaBuilderWrapper: Customer has not configured statistics.interval.ms, but config is IEnumerable (read-only)");
                return false;
            }

            // Try dictionary-style access as fallback
            dynamic dynamicConfig = config;
            if (dynamicConfig.ContainsKey("statistics.interval.ms"))
            {
                var existingValue = dynamicConfig["statistics.interval.ms"];
                Log.Debug($"KafkaBuilderWrapper: Customer has configured statistics.interval.ms = {existingValue}, respecting their setting");
                return true;
            }

            dynamicConfig["statistics.interval.ms"] = intervalMs.ToString();
            Log.Debug($"KafkaBuilderWrapper: Set statistics.interval.ms = {intervalMs} (customer had no existing config)");
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Config access failed: {ex.Message}");
            return false;
        }
    }

    private bool TryAddMethod(object config, int intervalMs)
    {
        try
        {
            var addMethod = config.GetType().GetMethod("Add", new[] { typeof(string), typeof(string) });
            if (addMethod != null)
            {
                addMethod.Invoke(config, new object[] { "statistics.interval.ms", intervalMs.ToString() });
                Log.Debug("KafkaBuilderWrapper: Add method succeeded");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Add method failed: {ex.Message}");
        }
        return false;
    }

    private bool TryReflectionConfigMethods(object builder, int intervalMs)
    {
        try
        {
            var methods = builder.GetType().GetMethods()
                .Where(m => m.Name.ToLower().Contains("config") || m.Name.ToLower().Contains("set"))
                .ToArray();

            Log.Debug($"KafkaBuilderWrapper: Found {methods.Length} potential config methods: {string.Join(", ", methods.Select(m => m.Name))}");

            // This is a fallback - we're not calling them as they might not be the right methods
            // But this gives us insight into what methods are available
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Reflection config methods failed: {ex.Message}");
        }
        return false;
    }

    #endregion
}