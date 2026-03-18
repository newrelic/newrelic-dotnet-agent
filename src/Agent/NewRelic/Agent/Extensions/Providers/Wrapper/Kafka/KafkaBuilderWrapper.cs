// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    private static readonly TimeSpan DrainInitialDelay = TimeSpan.FromSeconds(5);

    private IAgent _currentAgent;
    private readonly ConcurrentDictionary<object, string> _latestStatisticsPerClient = new();
    private readonly ConcurrentDictionary<object, Dictionary<string, long>> _previousValuesPerClient = new();
    private int _drainStarted;

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

    #region Statistics Collection

    /// <summary>
    /// Sets up non-invasive statistics collection for Kafka metrics.
    /// Registers a lightweight callback that caches the latest JSON per client, and starts a
    /// single scheduled drain task that parses and reports metrics once per harvest interval.
    /// </summary>
    private void SetupStatisticsCollection(object builder, IAgent agent)
    {
        Log.Finest($"KafkaBuilderWrapper: SetupStatisticsCollection called for builder type: {builder?.GetType().Name}");

        var ourHandler = CreateMetricsReportingHandler(agent, builder);
        if (ourHandler == null)
        {
            Log.Debug("KafkaBuilderWrapper: Could not create statistics handler");
            return;
        }

        SetStatisticsHandlerOnBuilder(builder, ourHandler);
        SetStatisticsIntervalOnBuilder(builder);

        // Start the drain exactly once — subsequent builders just register their callbacks
        if (Interlocked.CompareExchange(ref _drainStarted, 1, 0) == 0)
        {
            var drainInterval = agent.Configuration.MetricsHarvestCycle;
            agent.GetExperimentalApi().SimpleSchedulingService
                .StartExecuteEvery(DrainAndReportMetrics, drainInterval, DrainInitialDelay);

            Log.Debug($"KafkaBuilderWrapper: Scheduled drain started (interval: {drainInterval.TotalSeconds}s)");
        }

        Log.Debug("KafkaBuilderWrapper: Statistics handler configured successfully");
    }

    /// <summary>
    /// Creates a reflection-based delegate compatible with Kafka's SetStatisticsHandler.
    /// The delegate simply caches the latest JSON string — no parsing on the callback thread.
    /// </summary>
    private object CreateMetricsReportingHandler(IAgent agent, object builder)
    {
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

        var expectedDelegateType = parameters[0].ParameterType;
        var methodToInvoke = typeof(KafkaBuilderWrapper).GetMethod(nameof(StatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = Delegate.CreateDelegate(expectedDelegateType, this, methodToInvoke);

        _currentAgent = agent;

        Log.Finest($"KafkaBuilderWrapper: Created statistics handler of type: {handler.GetType().Name}");
        return handler;
    }

    /// <summary>
    /// Called by librdkafka on every statistics interval. Must be fast — just caches the JSON per client.
    /// </summary>
    private void StatisticsHandlerMethod(object client, string json)
    {
        Log.Finest($"KafkaBuilderWrapper: Statistics callback, JSON length: {json?.Length ?? 0}");
        _latestStatisticsPerClient[client] = json;
    }

    /// <summary>
    /// Scheduled drain task. Runs once per interval on the agent scheduler thread.
    /// Iterates all tracked clients, parses the latest cached JSON for each,
    /// computes deltas for cumulative counters, and reports all metrics as gauges.
    /// </summary>
    private void DrainAndReportMetrics()
    {
        try
        {
            if (_currentAgent == null)
                return;

            var experimentalApi = _currentAgent.GetExperimentalApi();
            var reportedCount = 0;

            foreach (var clientEntry in _latestStatisticsPerClient)
            {
                if (!_latestStatisticsPerClient.TryRemove(clientEntry.Key, out var json))
                    continue;

                var metricsData = KafkaStatisticsHelper.ParseStatistics(json);
                if (metricsData?.IsValid != true)
                {
                    Log.Debug("KafkaBuilderWrapper: Failed to parse Kafka statistics or data is invalid");
                    continue;
                }

                Log.Debug($"KafkaBuilderWrapper: Draining stats - ClientId: {metricsData.ClientId}, Type: {metricsData.ClientType}");

                var metricsDict = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, MessageBrokerVendorConstants.Kafka);
                var previousValues = _previousValuesPerClient.GetOrAdd(clientEntry.Key, _ => new Dictionary<string, long>(metricsDict.Count));

                foreach (var kvp in metricsDict)
                {
                    float valueToReport;

                    if (kvp.Value.MetricType == KafkaMetricType.Cumulative)
                    {
                        if (previousValues.TryGetValue(kvp.Key, out var prev))
                        {
                            var delta = kvp.Value.Value - prev;
                            // Handle counter reset: if delta is negative, report the raw value
                            valueToReport = delta >= 0 ? delta : kvp.Value.Value;
                        }
                        else
                        {
                            // First observation — report raw value (counters start at 0, so raw == delta from 0)
                            valueToReport = kvp.Value.Value;
                        }

                        previousValues[kvp.Key] = kvp.Value.Value;
                    }
                    else
                    {
                        // Gauge and WindowAvg: report raw value
                        valueToReport = kvp.Value.Value;
                    }

                    if (valueToReport > 0)
                    {
                        experimentalApi.RecordGaugeMetric(kvp.Key, valueToReport);
                        reportedCount++;
                    }
                }
            }

            if (reportedCount > 0)
                Log.Finest($"KafkaBuilderWrapper: Reported {reportedCount} Kafka metrics");
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Error draining Kafka metrics: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets statistics handler on the Kafka builder using reflection.
    /// </summary>
    private void SetStatisticsHandlerOnBuilder(object builder, object handler)
    {
        var setStatisticsMethods = builder.GetType().GetMethods()
            .Where(m => m.Name == "SetStatisticsHandler")
            .ToArray();

        if (setStatisticsMethods.Length > 0)
        {
            var method = setStatisticsMethods[0];
            method.Invoke(builder, new object[] { handler });
            Log.Finest("KafkaBuilderWrapper: SetStatisticsHandler invoked successfully");
        }
        else
        {
            Log.Info($"KafkaBuilderWrapper: No SetStatisticsHandler methods found on builder type {builder.GetType()}");
        }
    }

    /// <summary>
    /// Enables statistics on the Kafka builder if not already configured by the customer.
    /// </summary>
    private void SetStatisticsIntervalOnBuilder(object builder)
    {
        if (!ShouldSetStatisticsInterval(builder))
            return;

        var setConfigMethod = builder.GetType().GetMethod("SetConfig", new[] { typeof(string), typeof(string) });
        if (setConfigMethod != null)
        {
            setConfigMethod.Invoke(builder, new object[] { "statistics.interval.ms", "5000" });
            Log.Finest("KafkaBuilderWrapper: Set statistics interval to 5000ms");
        }
    }

    private bool ShouldSetStatisticsInterval(object builder)
    {
        try
        {
            if (!_builderConfigGetterDictionary.TryGetValue(builder.GetType(), out var configGetter))
            {
                configGetter = VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(builder.GetType(), "Config");
                _builderConfigGetterDictionary[builder.GetType()] = configGetter;
            }

            var config = configGetter(builder);
            if (config == null)
                return true;

            foreach (dynamic kvp in config)
            {
                if (kvp.Key == "statistics.interval.ms")
                {
                    var value = kvp.Value?.ToString();
                    if (!string.IsNullOrEmpty(value) && value != "0")
                    {
                        Log.Debug($"KafkaBuilderWrapper: Customer has already configured statistics interval to {value}ms");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Debug($"KafkaBuilderWrapper: Could not check existing statistics interval: {ex.Message}");
            return false;
        }
    }




    #endregion
}
