// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Caching;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Kafka;

public class KafkaBuilderWrapper : IWrapper
{
    private readonly ConcurrentDictionary<Type, Func<object, IEnumerable>> _builderConfigGetterDictionary = new();

    private const string WrapperName = "KafkaBuilderWrapper";
    private const string BootstrapServersKey = "bootstrap.servers";
    private static readonly TimeSpan DrainInitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ClientTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(2);

    private IAgent _currentAgent;
    // Per-client state is keyed by WeakReferenceKey<object> so that disposing a Kafka client
    // does not hold it rooted until TTL eviction fires. Stale dictionary entries (whose weak
    // target has been collected) are swept during the periodic TTL cleanup pass.
    private readonly ConcurrentDictionary<WeakReferenceKey<object>, string> _latestStatisticsPerClient = new();
    private readonly ConcurrentDictionary<WeakReferenceKey<object>, Dictionary<string, long>> _previousValuesPerClient = new();
    private readonly ConcurrentDictionary<WeakReferenceKey<object>, Dictionary<string, KafkaMetricValue>> _metricsPerClient = new();
    private readonly ConcurrentDictionary<WeakReferenceKey<object>, long> _previousTsPerClient = new();
    private readonly ConcurrentDictionary<Type, Func<object, object, object>> _setStatisticsCallerCache = new();
    private readonly ConcurrentDictionary<Type, Func<object, object>> _statisticsHandlerGetterCache = new();
    private readonly ConcurrentDictionary<Type, Action<object, object>> _statisticsHandlerFieldWriterCache = new();
    private readonly ConcurrentDictionary<WeakReferenceKey<object>, DateTime> _clientLastSeen = new();
    private int _drainStarted;
    private volatile bool _metricsCollectionDisabled;
    private DateTime _lastCleanupTime;

    public bool IsTransactionRequired => false;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(instrumentedMethodInfo.RequestedWrapperName));
    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var builder = instrumentedMethodCall.MethodCall.InvocationTarget;

        Log.Finest("KafkaBuilderWrapper: BeforeWrappedMethod called for builder type: {0}", builder?.GetType().Name);

        var configuration = GetBuilderConfig(builder);
        string bootstrapServers = null;

        try
        {
            foreach (KeyValuePair<string, string> kvp in configuration)
            {
                if (kvp.Key == BootstrapServersKey)
                {
                    bootstrapServers = kvp.Value;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("KafkaBuilderWrapper: Could not read bootstrap servers from builder config: {0}", ex.Message);
        }

        Log.Finest("KafkaBuilderWrapper: Found bootstrap servers: {0}", bootstrapServers ?? "null");

        // Set up statistics collection BEFORE Build() is called
        SetupStatisticsCollection(builder, agent);

        return Delegates.GetDelegateFor<object>(onSuccess: (clientAsObject) => {
            Log.Debug("KafkaBuilderWrapper: Build completed, client type: {0}", clientAsObject?.GetType().Name);

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
        Log.Finest("KafkaBuilderWrapper: SetupStatisticsCollection called for builder type: {0}", builder?.GetType().Name);

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
            // drain interval matches the agent's metric harvest cycle, but the drain runs independently on our own scheduler thread.
            var drainInterval = agent.Configuration.MetricsHarvestCycle;
            agent.GetExperimentalApi().SimpleSchedulingService
                .StartExecuteEvery(DrainAndReportMetrics, drainInterval, DrainInitialDelay);

            Log.Debug("KafkaBuilderWrapper: Scheduled drain started (interval: {0}s)", drainInterval.TotalSeconds);
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
            Log.Info("KafkaBuilderWrapper: No SetStatisticsHandler method found on {0}", builderType.Name);
            return null;
        }

        var parameters = setStatisticsMethod.GetParameters();
        if (parameters.Length != 1)
        {
            Log.Info("KafkaBuilderWrapper: SetStatisticsHandler has unexpected parameter count: {0}", parameters.Length);
            return null;
        }

        var expectedDelegateType = parameters[0].ParameterType;
        var methodToInvoke = typeof(KafkaBuilderWrapper).GetMethod(nameof(StatisticsHandlerMethod), BindingFlags.Instance | BindingFlags.NonPublic);
        var handler = Delegate.CreateDelegate(expectedDelegateType, this, methodToInvoke);

        _currentAgent = agent;

        Log.Debug("KafkaBuilderWrapper: Created statistics handler of type: {0}", handler.GetType().Name);
        return handler;
    }

    /// <summary>
    /// Called by librdkafka on every statistics interval. Must be fast — just caches the JSON per client.
    /// Allocates a new WeakReferenceKey per callback; if the client has already been seen, the dictionary
    /// keeps its original key (hashcode+Equals match) and only updates the value, so the transient key is
    /// immediately eligible for GC. Same allocation pattern as AwsSdk/AmazonServiceClientWrapper.
    /// </summary>
    private void StatisticsHandlerMethod(object client, string json)
    {
        if (_metricsCollectionDisabled)
            return;

        Log.Finest("KafkaBuilderWrapper: Statistics callback, JSON length: {0}", json?.Length ?? 0);
        _latestStatisticsPerClient[new WeakReferenceKey<object>(client)] = json;
    }

    /// <summary>
    /// Scheduled drain task. Runs once per interval on the agent scheduler thread.
    /// Iterates all tracked clients, parses the latest cached JSON for each,
    /// computes deltas for cumulative counters, and reports all metrics as gauges.
    /// Also runs periodic TTL eviction (every 2 minutes) to remove state for clients
    /// that have not reported statistics in the last 5 minutes.
    ///
    /// A single outer try/catch is deliberate: any exception — whether tied to one client
    /// or the experimental API as a whole — disables metrics collection for the remainder
    /// of this agent's lifetime. This is preferred over logging per failure: repeatedly
    /// logging the same error floods customer logs, whereas disabling surfaces as a
    /// customer bug report that we can then investigate directly.
    /// </summary>
    private void DrainAndReportMetrics()
    {
        if (_metricsCollectionDisabled || _currentAgent == null)
            return;

        try
        {
            var experimentalApi = _currentAgent.GetExperimentalApi();
            var reportedCount = 0;

            foreach (var clientEntry in _latestStatisticsPerClient)
            {
                if (!_latestStatisticsPerClient.TryRemove(clientEntry.Key, out var json))
                    continue;

                Log.Debug("KafkaBuilderWrapper: Raw statistics JSON: {0}", json);

                var stats = KafkaStatisticsHelper.ParseStatistics(json);
                if (!KafkaStatisticsHelper.IsValid(stats))
                {
                    Log.Debug("KafkaBuilderWrapper: Failed to parse Kafka statistics or data is invalid");
                    continue;
                }

                Log.Debug("KafkaBuilderWrapper: Draining stats - ClientId: {0}, Type: {1}", KafkaStatisticsHelper.GetClientId(stats), stats.Type);

                var metricsDict = _metricsPerClient.GetOrAdd(clientEntry.Key, _ => new Dictionary<string, KafkaMetricValue>());
                KafkaStatisticsHelper.PopulateMetricsDictionary(metricsDict, stats);
                var previousValues = _previousValuesPerClient.GetOrAdd(clientEntry.Key, _ => new Dictionary<string, long>(metricsDict.Count));

                // Elapsed seconds derived from librdkafka's own monotonic clock (ts, in microseconds).
                // Both the delta numerator and this denominator come from the same librdkafka snapshot pair,
                // so rates are independent of our scheduler's timing accuracy.
                var previousTs = _previousTsPerClient.TryGetValue(clientEntry.Key, out var pts) ? pts : 0L;
                double elapsedSeconds;

                if (stats.Ts == 0)
                {
                    // ts is documented in librdkafka STATISTICS.md as an ever-increasing monotonic clock
                    // (microseconds). A zero value means the field is absent or unsupported by this version
                    // of librdkafka. Rate metrics cannot be computed without it.
                    Log.Debug("KafkaBuilderWrapper: Statistics payload for client '{0}' has no 'ts' timestamp field — rate metrics will not be reported for this client.", KafkaStatisticsHelper.GetClientId(stats));
                    elapsedSeconds = 0.0;
                }
                else if (previousTs == 0)
                {
                    // First observation for this client — no prior ts to form a delta against. Normal on startup.
                    elapsedSeconds = 0.0;
                }
                else
                {
                    elapsedSeconds = (stats.Ts - previousTs) / 1_000_000.0;
                }

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

                            // Rate = delta / elapsed seconds. Only emit when both observations are real
                            // and elapsed time is meaningful (guards first observation and ts=0 payloads).
                            if (elapsedSeconds > 0 && delta > 0)
                            {
                                experimentalApi.RecordGaugeMetric(ToRateMetricName(kvp.Key), (float)(delta / elapsedSeconds));
                                reportedCount++;
                            }
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

                if (stats.Ts > 0)
                    _previousTsPerClient[clientEntry.Key] = stats.Ts;

                _clientLastSeen[clientEntry.Key] = DateTime.UtcNow;
            }

            if (reportedCount > 0)
                Log.Finest("KafkaBuilderWrapper: Reported {0} Kafka metrics", reportedCount);

            var now = DateTime.UtcNow;
            if (now - _lastCleanupTime >= CleanupInterval)
            {
                _lastCleanupTime = now;
                var cutoff = now - ClientTtl;
                foreach (var entry in _clientLastSeen)
                {
                    // Evict if the client has been collected (weak ref dead) or has gone quiet past TTL.
                    if (entry.Key.Value == null || entry.Value < cutoff)
                    {
                        _clientLastSeen.TryRemove(entry.Key, out _);
                        _previousValuesPerClient.TryRemove(entry.Key, out _);
                        _previousTsPerClient.TryRemove(entry.Key, out _);
                        _metricsPerClient.TryRemove(entry.Key, out _);
                        _latestStatisticsPerClient.TryRemove(entry.Key, out _);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Info("KafkaBuilderWrapper: Unrecoverable error in metrics drain — disabling Kafka metrics collection: {0}", ex.Message);
            _metricsCollectionDisabled = true;
            _latestStatisticsPerClient.Clear();
            _previousValuesPerClient.Clear();
            _previousTsPerClient.Clear();
            _metricsPerClient.Clear();
            _clientLastSeen.Clear();
            _currentAgent?.GetExperimentalApi().SimpleSchedulingService.StopExecuting(DrainAndReportMetrics);
        }
    }

    /// <summary>
    /// Installs our statistics handler on the Kafka builder. If the customer has already set a
    /// statistics handler, our handler is composed with theirs via Delegate.Combine and the
    /// composite is written directly to the backing field — Confluent.Kafka's public
    /// SetStatisticsHandler throws on a second call, so we bypass it in that case.
    /// Ours is placed first in the invocation list so our metric caching always runs even if
    /// the customer's handler later throws.
    /// </summary>
    private void SetStatisticsHandlerOnBuilder(object builder, object ourHandler)
    {
        var builderType = builder.GetType();
        var existingHandler = TryGetExistingStatisticsHandler(builderType, builder);

        if (existingHandler != null)
        {
            var combined = Delegate.Combine((Delegate)ourHandler, (Delegate)existingHandler);
            if (TryWriteStatisticsHandlerField(builderType, builder, combined))
            {
                Log.Debug("KafkaBuilderWrapper: Composed our statistics handler with customer's existing handler on {0}", builderType.Name);
            }
            else
            {
                Log.Info("KafkaBuilderWrapper: Customer statistics handler present and field-level composition failed — internal Kafka metrics will not be collected for this client");
            }
            return;
        }

        // No existing handler — use the public SetStatisticsHandler method.
        var caller = _setStatisticsCallerCache.GetOrAdd(builderType, t =>
        {
            try
            {
                return VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<object, object>(
                    t.Assembly.GetName().Name, t.FullName, "SetStatisticsHandler");
            }
            catch
            {
                return null;
            }
        });

        if (caller == null)
        {
            Log.Info("KafkaBuilderWrapper: No SetStatisticsHandler method found on builder type {0}", builderType);
            return;
        }

        try
        {
            caller(builder, ourHandler);
            Log.Finest("KafkaBuilderWrapper: SetStatisticsHandler invoked successfully");
        }
        catch (Exception ex)
        {
            Log.Info("KafkaBuilderWrapper: SetStatisticsHandler failed on {0}: {1} — internal Kafka metrics will not be collected for this client", builderType.Name, ex.Message);
        }
    }

    private object TryGetExistingStatisticsHandler(Type builderType, object builder)
    {
        try
        {
            var getter = _statisticsHandlerGetterCache.GetOrAdd(builderType, t =>
                VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "StatisticsHandler"));
            return getter(builder);
        }
        catch (Exception ex)
        {
            Log.Debug("KafkaBuilderWrapper: Could not read existing StatisticsHandler from {0}: {1}", builderType.Name, ex.Message);
            return null;
        }
    }

    private bool TryWriteStatisticsHandlerField(Type builderType, object builder, Delegate value)
    {
        try
        {
            // Auto-property backing field name — the C# compiler emits "<PropertyName>k__BackingField"
            // for auto-properties. Stable across modern C# compilers.
            var writer = _statisticsHandlerFieldWriterCache.GetOrAdd(builderType, t =>
                VisibilityBypasser.Instance.GenerateFieldWriteAccessor<object>(t, "<StatisticsHandler>k__BackingField"));
            writer(builder, value);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("KafkaBuilderWrapper: Could not write StatisticsHandler backing field on {0}: {1}", builderType.Name, ex.Message);
            return false;
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

    /// <summary>
    /// Returns the builder's Config enumerable via a cached property accessor. Used both when
    /// reading bootstrap servers and when checking whether the customer has already configured
    /// a statistics interval.
    /// </summary>
    private IEnumerable GetBuilderConfig(object builder)
    {
        var configGetter = _builderConfigGetterDictionary.GetOrAdd(builder.GetType(),
            t => VisibilityBypasser.Instance.GeneratePropertyAccessor<IEnumerable>(t, "Config"));
        return configGetter(builder);
    }

    /// <summary>
    /// Derives a rate metric name from a cumulative metric name by replacing the trailing
    /// aggregation suffix (-total, -counter, -count) with -rate, or appending -rate if none match.
    /// Examples: "outgoing-byte-total" → "outgoing-byte-rate", "request-counter" → "request-rate".
    /// </summary>
    private static string ToRateMetricName(string metricName)
    {
        if (metricName.EndsWith("-total"))
            return metricName.Substring(0, metricName.Length - 6) + "-rate";
        if (metricName.EndsWith("-counter"))
            return metricName.Substring(0, metricName.Length - 8) + "-rate";
        if (metricName.EndsWith("-count"))
            return metricName.Substring(0, metricName.Length - 6) + "-rate";
        return metricName + "-rate";
    }

    private bool ShouldSetStatisticsInterval(object builder)
    {
        try
        {
            var config = GetBuilderConfig(builder);
            if (config == null)
                return true;

            foreach (KeyValuePair<string, string> kvp in config)
            {
                if (kvp.Key == "statistics.interval.ms")
                {
                    var value = kvp.Value;
                    if (!string.IsNullOrEmpty(value) && value != "0")
                    {
                        Log.Debug("KafkaBuilderWrapper: Customer has already configured statistics interval to {0}ms", value);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("KafkaBuilderWrapper: Could not check existing statistics interval: {0}", ex.Message);
            return false;
        }
    }

    #endregion
}
