// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.AgentHealth
{
    public class AgentHealthReporter : ConfigurationBasedService, IAgentHealthReporter
    {
        private static readonly TimeSpan _timeBetweenExecutions = TimeSpan.FromMinutes(2);

        private readonly IMetricBuilder _metricBuilder;
        private readonly IScheduler _scheduler;
        private readonly IFileWrapper _fileWrapper;
        private readonly IDirectoryWrapper _directoryWrapper;
        private readonly IList<string> _recurringLogData = new ConcurrentList<string>();
        private readonly IDictionary<AgentHealthEvent, InterlockedCounter> _agentHealthEventCounters = new Dictionary<AgentHealthEvent, InterlockedCounter>();
        private readonly ConcurrentDictionary<string, InterlockedCounter> _logLinesCountByLevel = new ConcurrentDictionary<string, InterlockedCounter>();
        private readonly ConcurrentDictionary<string, InterlockedCounter> _logDeniedCountByLevel = new ConcurrentDictionary<string, InterlockedCounter>();

        private PublishMetricDelegate _publishMetricDelegate;
        private InterlockedCounter _payloadCreateSuccessCounter;
        private InterlockedCounter _payloadAcceptSuccessCounter;

        private InterlockedCounter _traceContextCreateSuccessCounter;
        private InterlockedCounter _traceContextAcceptSuccessCounter;
        private InterlockedCounter _distributedTraceHeadersAcceptedLateCounter = new InterlockedCounter();

        private readonly InterlockedCounter _customInstrumentationCounter;
        private readonly HashSet<string> _customInstrumentationIds = new();
        private bool _customInstrumentationMaxLogged;

        private HealthCheck _healthCheck;
        private bool _healthChecksInitialized;
        private bool _healthChecksFailed;
        private string _healthCheckPath;

        public AgentHealthReporter(IMetricBuilder metricBuilder, IScheduler scheduler, IFileWrapper fileWrapper, IDirectoryWrapper directoryWrapper)
        {
            _metricBuilder = metricBuilder;
            _scheduler = scheduler;
            _fileWrapper = fileWrapper;
            _directoryWrapper = directoryWrapper;

            _subscriptions.Add<AgentConnectedEvent>(e => OnAgentConnected());

            var agentHealthEvents = Enum.GetValues(typeof(AgentHealthEvent)) as AgentHealthEvent[];
            foreach (var agentHealthEvent in agentHealthEvents)
            {
                _agentHealthEventCounters[agentHealthEvent] = new InterlockedCounter();
            }

            _payloadCreateSuccessCounter = new InterlockedCounter();
            _payloadAcceptSuccessCounter = new InterlockedCounter();
            _traceContextAcceptSuccessCounter = new InterlockedCounter();
            _traceContextCreateSuccessCounter = new InterlockedCounter();
            _customInstrumentationCounter = new InterlockedCounter();

            if (_configuration.AgentControlEnabled)
                _healthCheck = new() { IsHealthy = true, Status = "Agent starting", LastError = string.Empty };
        }

        public override void Dispose()
        {
            _scheduler.StopExecuting(LogPeriodicReport);
            base.Dispose();
        }

        /// <summary>
        /// Handles the event when the agent is connected, initializing health checks and scheduling periodic tasks.
        /// </summary>
        /// <remarks>
        /// This method starts the heartbeat timer to log periodic reports and, if agent control
        /// is enabled, schedules health checks to be published at regular intervals. It also performs immediate health
        /// status checks for critical conditions, such as missing license keys or application names, and updates the
        /// agent control status accordingly.
        ///
        /// public for unit testing
        /// </remarks>
        public void OnAgentConnected()
        {
            Log.Debug("AgentHealthReporter: Agent is connected. Initializing health checks.");

            // start the heartbeat timer
            _scheduler.ExecuteEvery(LogPeriodicReport, _timeBetweenExecutions);

            if (!_configuration.AgentControlEnabled)
                Log.Debug("Agent Control is disabled. Health checks will not be reported.");
            else
            {
                Log.Debug("Agent Control health checks will be published every {HealthCheckInterval} seconds", _configuration.HealthFrequency);

                // report a few things immediately -- these used to be reported in DefaultConfiguration but we removed the dependency on AgentHealthReporter from it
                // we can only report one of these things, so order from most to least important
                if (string.IsNullOrWhiteSpace(_configuration.AgentLicenseKey) && !_configuration.ServerlessModeEnabled)
                {
                    SetAgentControlStatus(HealthCodes.LicenseKeyMissing);
                }
                else if (_configuration.ApplicationNamesMissing)
                {
                    SetAgentControlStatus(HealthCodes.ApplicationNameMissing);
                }

                // schedule the health check and issue the first one immediately
                _scheduler.ExecuteEvery(PublishAgentControlHealthCheck, TimeSpan.FromSeconds(_configuration.HealthFrequency), TimeSpan.Zero);
            }
        }

        private void LogPeriodicReport()
        {
            foreach (var logMessage in _recurringLogData)
            {
                Log.Debug(logMessage);
            }
            List<string> events = new List<string>();
            foreach (var counter in _agentHealthEventCounters)
            {
                if (counter.Value != null && counter.Value.Value > 0)
                {
                    var agentHealthEvent = counter.Key;
                    var timesOccured = counter.Value.Exchange(0);
                    events.Add(string.Format("{0} {1} {2}", timesOccured, agentHealthEvent, (timesOccured == 1) ? "event" : "events"));
                }
            }
            var message = events.Count > 0 ? string.Join(", ", events) : "No events";
            Log.Info($"AgentHealthReporter: In the last {_timeBetweenExecutions.TotalMinutes} minutes: {message}");
        }

        public void ReportSupportabilityCountMetric(string metricName, long count = 1)
        {
            var metric = _metricBuilder.TryBuildSupportabilityCountMetric(metricName, count);
            TrySend(metric);
        }

        public void ReportSupportabilityDataUsageMetric(string metricName, long callCount, float bytesSent, float bytesReceived)
        {
            var metric = _metricBuilder.TryBuildSupportabilityDataUsageMetric(metricName, callCount, bytesSent, bytesReceived);
            TrySend(metric);
        }

        private void ReportSupportabilitySummaryMetric(string metricName, float totalSize, int countSamples, float minValue, float maxValue)
        {
            var metric = _metricBuilder.TryBuildSupportabilitySummaryMetric(metricName, totalSize, countSamples, minValue, maxValue);
            TrySend(metric);
        }


        private void ReportSupportabilityGaugeMetric(string metricName, float value)
        {
            var metric = _metricBuilder.TryBuildSupportabilityGaugeMetric(metricName, value);
            TrySend(metric);
        }

        public void ReportCountMetric(string metricName, long count)
        {
            var metric = _metricBuilder.TryBuildCountMetric(metricName, count);
            TrySend(metric);
        }
        public void ReportByteMetric(string metricName, long totalBytes, long? exclusiveBytes = null)
        {
            var metric = _metricBuilder.TryBuildByteMetric(metricName, totalBytes, exclusiveBytes);
            TrySend(metric);
        }



        public void ReportDotnetVersion()
        {
#if NETFRAMEWORK
            var metric = _metricBuilder.TryBuildDotnetFrameworkVersionMetric(AgentInstallConfiguration.DotnetFrameworkVersion);
#else
            var metric = _metricBuilder.TryBuildDotnetCoreVersionMetric(AgentInstallConfiguration.DotnetCoreVersion);
#endif
            TrySend(metric);
        }

        public void ReportAgentVersion(string agentVersion)
        {
            TrySend(_metricBuilder.TryBuildAgentVersionMetric(agentVersion));
        }

        public void ReportLibraryVersion(string assemblyName, string assemblyVersion)
        {
            TrySend(_metricBuilder.TryBuildLibraryVersionMetric(assemblyName, assemblyVersion));
        }

        public void ReportCustomInstrumentation(string assemblyName, string className, string method)
        {
            // record only unique custom instrumentation metrics
            var uniqueIdentifier = $"{assemblyName}.{className}.{method}";
            if (!_customInstrumentationIds.Add(uniqueIdentifier))
            {
                return;
            }

            // always report the custom instrumentation count metric
            _customInstrumentationCounter.Increment();

            // if the custom instrumentation count exceeds the maximum allowed, log a warning and stop reporting further custom instrumentation metrics
            if (_customInstrumentationCounter.Value > _configuration.MaxCustomInstrumentationSupportabilityMetrics)
            {
                if (!_customInstrumentationMaxLogged)
                {
                    Log.Debug($"Custom instrumentation count {_customInstrumentationCounter.Value} has exceeded the maximum of {_configuration.MaxCustomInstrumentationSupportabilityMetrics}. No further custom instrumentation metrics will be reported.");
                    _customInstrumentationMaxLogged = true;
                }
                return;
            }

            TrySend(_metricBuilder.TryBuildCustomInstrumentationMetric(assemblyName, className, method));
        }

        #region TransactionEvents

        public void ReportTransactionEventReservoirResized(int newSize)
        {
            TrySend(_metricBuilder.TryBuildTransactionEventReservoirResizedMetric());
            Log.Warn("Resizing transaction event reservoir to " + newSize + " events.");
        }

        public void ReportTransactionEventCollected()
        {
            TrySend(_metricBuilder.TryBuildTransactionEventsCollectedMetric());

            // Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
            TrySend(_metricBuilder.TryBuildTransactionEventsSeenMetric());
        }

        public void ReportTransactionEventsRecollected(int count) => TrySend(_metricBuilder.TryBuildTransactionEventsRecollectedMetric(count));

        public void ReportTransactionEventsSent(int count)
        {
            TrySend(_metricBuilder.TryBuildTransactionEventsSentMetric(count));
            _agentHealthEventCounters[AgentHealthEvent.Transaction]?.Add(count);
        }

        #endregion TransactionEvents

        #region CustomEvents

        public void ReportCustomEventReservoirResized(int newSize)
        {
            TrySend(_metricBuilder.TryBuildCustomEventReservoirResizedMetric());
            Log.Warn("Resizing custom event reservoir to " + newSize + " events.");
        }

        public void ReportCustomEventCollected()
        {
            TrySend(_metricBuilder.TryBuildCustomEventsCollectedMetric());

            // Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
            TrySend(_metricBuilder.TryBuildCustomEventsSeenMetric());
        }

        public void ReportCustomEventsRecollected(int count) => TrySend(_metricBuilder.TryBuildCustomEventsRecollectedMetric(count));

        // Note: Though not required by APM like the transaction event supportability metrics, this metric should still be created to maintain consistency
        public void ReportCustomEventsSent(int count)
        {
            TrySend(_metricBuilder.TryBuildCustomEventsSentMetric(count));
            _agentHealthEventCounters[AgentHealthEvent.Custom]?.Add(count);

        }

        #endregion CustomEvents

        #region ErrorTraces

        public void ReportErrorTraceCollected() => TrySend(_metricBuilder.TryBuildErrorTracesCollectedMetric());

        public void ReportErrorTracesRecollected(int count) => TrySend(_metricBuilder.TryBuildErrorTracesRecollectedMetric(count));

        public void ReportErrorTracesSent(int count) => TrySend(_metricBuilder.TryBuildErrorTracesSentMetric(count));

        #endregion ErrorTraces

        #region ErrorEvents

        public void ReportErrorEventSeen() => TrySend(_metricBuilder.TryBuildErrorEventsSeenMetric());

        public void ReportErrorEventsSent(int count)
        {
            TrySend(_metricBuilder.TryBuildErrorEventsSentMetric(count));
            _agentHealthEventCounters[AgentHealthEvent.Error]?.Add(count);
        }

        #endregion ErrorEvents

        #region SqlTraces

        public void ReportSqlTracesRecollected(int count) => TrySend(_metricBuilder.TryBuildSqlTracesRecollectedMetric(count));

        public void ReportSqlTracesSent(int count) => TrySend(_metricBuilder.TryBuildSqlTracesSentMetric(count));

        #endregion ErrorTraces

        public void ReportAgentInfo()
        {
            TrySend(_metricBuilder.TryBuildInstallTypeMetric(AgentInstallConfiguration.AgentInfo?.ToString() ?? "Unknown"));
        }

        public void ReportTransactionGarbageCollected(string transactionGuid, TransactionMetricName transactionMetricName, string lastStartedSegmentName, string lastFinishedSegmentName)
        {
            var transactionName = transactionMetricName.PrefixedName;
            Log.Debug($"Transaction was garbage collected without ever ending.\nTransaction Guid: {transactionGuid}\nTransaction Name: {transactionName}\nLast Started Segment: {lastStartedSegmentName}\nLast Finished Segment: {lastFinishedSegmentName}");
            _agentHealthEventCounters[AgentHealthEvent.TransactionGarbageCollected]?.Increment();
        }

        public void ReportWrapperShutdown(IWrapper wrapper, Method method)
        {
            var wrapperName = wrapper.GetType().FullName;
            var metrics = new[]
            {
                _metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, "all"),
                _metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, $"{wrapperName}/all"),
                _metricBuilder.TryBuildAgentHealthEventMetric(AgentHealthEvent.WrapperShutdown, wrapperName, method.Type.Name, method.MethodName)
            };

            foreach (var metric in metrics)
            {
                TrySend(metric);
            }

            Log.Error($"Wrapper {wrapperName} is being disabled for {method.MethodName} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted.");
            _recurringLogData.Add($"Wrapper {wrapperName} was disabled for {method.MethodName} at {DateTime.Now} due to too many consecutive exceptions. All other methods using this wrapper will continue to be instrumented. This will reduce the functionality of the agent until the agent is restarted.");
        }

        public void ReportIfHostIsLinuxOs()
        {
#if NETSTANDARD2_0

            bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
            var metric = _metricBuilder.TryBuildLinuxOsMetric(isLinux);
            TrySend(metric);
#endif
        }

        public void ReportBootIdError() => TrySend(_metricBuilder.TryBuildBootIdError());

        public void ReportKubernetesUtilizationError() => TrySend(_metricBuilder.TryBuildKubernetesUsabilityError());

        public void ReportAwsUtilizationError() => TrySend(_metricBuilder.TryBuildAwsUsabilityError());

        public void ReportAzureUtilizationError() => TrySend(_metricBuilder.TryBuildAzureUsabilityError());

        public void ReportPcfUtilizationError() => TrySend(_metricBuilder.TryBuildPcfUsabilityError());

        public void ReportGcpUtilizationError() => TrySend(_metricBuilder.TryBuildGcpUsabilityError());

        #region DistributedTrace

        /// <summary>Incremented when AcceptDistributedTracePayload was called successfully</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadSuccess()
        {
            _payloadAcceptSuccessCounter.Increment();
        }

        /// <summary>Created when AcceptDistributedTracePayload had a generic exception</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadException() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadException);

        /// <summary>Created when AcceptDistributedTracePayload had a parsing exception</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadParseException() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadParseException);

        /// <summary>Created when AcceptDistributedTracePayload was ignored because CreatePayload had already been called</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredCreateBeforeAccept);

        /// <summary>Created when AcceptDistributedTracePayload was ignored because AcceptPayload had already been called</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredMultiple);

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload's major version was greater than the agent's</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredMajorVersion);

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was null</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredNull);

        /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was untrusted</summary>
        public void ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount() =>
            TrySend(_metricBuilder.TryBuildAcceptPayloadIgnoredUntrustedAccount());

        /// <summary>Incremented when CreateDistributedTracePayload was called successfully</summary>
        public void ReportSupportabilityDistributedTraceCreatePayloadSuccess()
        {
            _payloadCreateSuccessCounter.Increment();
        }

        /// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
        public void ReportSupportabilityDistributedTraceCreatePayloadException() =>
            TrySend(_metricBuilder.TryBuildCreatePayloadException);

        public void ReportSupportabilityDistributedTraceHeadersAcceptedLate()
        {
            _distributedTraceHeadersAcceptedLateCounter.Increment();
        }

        /// <summary>Limits Collect calls to once per harvest per metric.</summary>
        public void CollectDistributedTraceMetrics()
        {
            if (TryGetCount(_payloadCreateSuccessCounter, out var createCount))
            {
                TrySend(_metricBuilder.TryBuildCreatePayloadSuccess(createCount));
            }

            if (TryGetCount(_payloadAcceptSuccessCounter, out var acceptCount))
            {
                TrySend(_metricBuilder.TryBuildAcceptPayloadSuccess(acceptCount));
            }

            if (TryGetCount(_distributedTraceHeadersAcceptedLateCounter, out var acceptedLateCount))
            {
                TrySend(_metricBuilder.TryBuildDistributedTraceHeadersAcceptedLate(acceptedLateCount));
            }
        }

        #endregion DistributedTrace

        #region TraceContext

        /// <summary>Incremented when the agent successfuly processes inbound tracestate and traceparent headers</summary>
        public void ReportSupportabilityTraceContextAcceptSuccess()
        {
            _traceContextAcceptSuccessCounter.Increment();
        }

        /// <summary>Incremented when the agent successfully creates outbound trace context payloads</summary>
        public void ReportSupportabilityTraceContextCreateSuccess()
        {
            _traceContextCreateSuccessCounter.Increment();
        }

        /// <summary>Created when a generic exception occurred unrelated to parsing while accepting either payload.</summary>
        public void ReportSupportabilityTraceContextAcceptException() =>
            TrySend(_metricBuilder.TryBuildTraceContextAcceptException);

        /// <summary>Created when the inbound traceparent header could not be parsed.</summary>
        public void ReportSupportabilityTraceContextTraceParentParseException() =>
            TrySend(_metricBuilder.TryBuildTraceContextTraceParentParseException);

        /// <summary>Created when the inbound tracestate header could not be parsed.</summary>
        public void ReportSupportabilityTraceContextTraceStateParseException() =>
            TrySend(_metricBuilder.TryBuildTraceContextTraceStateParseException);

        /// <summary>Created when a generic exception occurred while creating the outbound payloads.</summary>
        public void ReportSupportabilityTraceContextCreateException() =>
            TrySend(_metricBuilder.TryBuildTraceContextCreateException);

        /// <summary>Created when the inbound tracestate header exists, and was accepted, but the New Relic entry was invalid.</summary>
        public void ReportSupportabilityTraceContextTraceStateInvalidNrEntry() =>
            TrySend(_metricBuilder.TryBuildTraceContextTraceStateInvalidNrEntry);

        /// <summary>Created when the traceparent header exists, and was accepted, but the tracestate header did not contain a trusted New Relic entry.</summary>
        public void ReportSupportabilityTraceContextTraceStateNoNrEntry() =>
            TrySend(_metricBuilder.TryBuildTraceContextTraceStateNoNrEntry);

        /// <summary>Limits Collect calls to once per harvest per metric.</summary>
        public void CollectTraceContextSuccessMetrics()
        {
            if (TryGetCount(_traceContextCreateSuccessCounter, out var createCount))
            {
                TrySend(_metricBuilder.TryBuildTraceContextCreateSuccess(createCount));
            }

            if (TryGetCount(_traceContextAcceptSuccessCounter, out var acceptCount))
            {
                TrySend(_metricBuilder.TryBuildTraceContextAcceptSuccess(acceptCount));
            }
        }

        #endregion TraceContext

        #region Span 

        public void ReportSpanEventCollected(int count) => TrySend(_metricBuilder.TryBuildSpanEventsSeenMetric(count));

        public void ReportSpanEventsSent(int count)
        {
            TrySend(_metricBuilder.TryBuildSpanEventsSentMetric(count));
            _agentHealthEventCounters[AgentHealthEvent.Span]?.Add(count);
        }

        #endregion Span 

        #region InfiniteTracing

        private InterlockedLongCounter _infiniteTracingSpanResponseError = new InterlockedLongCounter();
        public void ReportInfiniteTracingSpanResponseError()
        {
            _infiniteTracingSpanResponseError.Increment();
        }

        public void ReportInfiniteTracingSpanQueueSize(int queueSize)
        {
            ReportSupportabilityGaugeMetric(MetricNames.SupportabilityInfiniteTracingSpanQueueSize, queueSize);
        }


        private InterlockedLongCounter _infiniteTracingSpanEventsDropped = new InterlockedLongCounter();
        public void ReportInfiniteTracingSpanEventsDropped(long countSpans)
        {
            _infiniteTracingSpanEventsDropped.Add(countSpans);
        }


        private InterlockedLongCounter _infiniteTracingSpanEventsSeen = new InterlockedLongCounter();
        public void ReportInfiniteTracingSpanEventsSeen(long countSpans)
        {
            _infiniteTracingSpanEventsSeen.Add(countSpans);
        }

        private InterlockedLongCounter _infiniteTracingSpanEventsSent = new InterlockedLongCounter();
        private InterlockedCounter _infiniteTracingSpanBatchCount = new InterlockedCounter();
        private long _infiniteTracingSpanBatchSizeMin = long.MaxValue;
        private long _infiniteTracingSpanBatchSizeMax = long.MinValue;

        private readonly object _syncRootMetrics = new object();

        public void ReportInfiniteTracingSpanEventsSent(long countSpans)
        {
            _infiniteTracingSpanEventsSent.Add(countSpans);
            _infiniteTracingSpanBatchCount.Increment();

            lock (_syncRootMetrics)
            {
                _infiniteTracingSpanBatchSizeMin = Math.Min(_infiniteTracingSpanBatchSizeMin, countSpans);
                _infiniteTracingSpanBatchSizeMax = Math.Max(_infiniteTracingSpanBatchSizeMax, countSpans);
            }
            _agentHealthEventCounters[AgentHealthEvent.InfiniteTracingSpan]?.Add((int)countSpans);

        }

        private InterlockedLongCounter _infiniteTracingSpanEventsReceived = new InterlockedLongCounter();
        public void ReportInfiniteTracingSpanEventsReceived(ulong countSpans)
        {
            _infiniteTracingSpanEventsReceived.Add(countSpans);
        }

        private ConcurrentDictionary<string, InterlockedLongCounter> _infiniteTracingSpanGrpcErrorCounters = new ConcurrentDictionary<string, InterlockedLongCounter>();
        public void ReportInfiniteTracingSpanGrpcError(string error)
        {
            var counter = _infiniteTracingSpanGrpcErrorCounters.GetOrAdd(error, CreateNewGrpcErrorInterlockedCounter);
            counter.Increment();
        }

        private InterlockedCounter _infiniteTracingSpanGrpcTimeout = new InterlockedCounter();
        public void ReportInfiniteTracingSpanGrpcTimeout()
        {
            _infiniteTracingSpanGrpcTimeout.Increment();
        }

        private static InterlockedLongCounter CreateNewGrpcErrorInterlockedCounter(string _)
        {
            return new InterlockedLongCounter();
        }

        private void CollectInfiniteTracingMetrics()
        {
            if (TryGetCount(_infiniteTracingSpanResponseError, out var errorCount))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanResponseError, errorCount);
            }

            if (TryGetCount(_infiniteTracingSpanEventsDropped, out var spansDropped))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanDropped, spansDropped);
            }

            if (TryGetCount(_infiniteTracingSpanEventsSeen, out var spanEventsSeen))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanSeen, spanEventsSeen);
            }

            if (TryGetCount(_infiniteTracingSpanEventsSent, out var spanEventsSent) && TryGetCount(_infiniteTracingSpanBatchCount, out var spanBatchCount))
            {

                var minBatchSize = Interlocked.Exchange(ref _infiniteTracingSpanBatchSizeMin, long.MaxValue);
                var maxBatchSize = Interlocked.Exchange(ref _infiniteTracingSpanBatchSizeMax, long.MinValue);

                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanSent, spanEventsSent);

                if (minBatchSize < long.MaxValue && maxBatchSize > long.MinValue)
                {
                    ReportSupportabilitySummaryMetric(MetricNames.SupportabilityInfiniteTracingSpanSentBatchSize, spanEventsSent, spanBatchCount, minBatchSize, maxBatchSize);
                }
            }

            if (TryGetCount(_infiniteTracingSpanEventsReceived, out var spanEventsReceived))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanReceived, spanEventsReceived);
            }

            if (TryGetCount(_infiniteTracingSpanGrpcTimeout, out var timeouts))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingSpanGrpcTimeout, timeouts);
            }

            foreach (var errorCounterPair in _infiniteTracingSpanGrpcErrorCounters)
            {
                if (TryGetCount(errorCounterPair.Value, out var grpcErrorCount))
                {
                    var metricName = MetricNames.SupportabilityInfiniteTracingSpanGrpcError(errorCounterPair.Key);
                    ReportSupportabilityCountMetric(metricName, grpcErrorCount);
                }
            }
        }

        private void ReportInfiniteTracingOneTimeMetrics()
        {
            ReportSupportabilityCountMetric(MetricNames.SupportabilityInfiniteTracingCompression(_configuration.InfiniteTracingCompression));
        }

        #endregion

        public void ReportAgentTimingMetric(string suffix, TimeSpan time)
        {
            var metric = _metricBuilder.TryBuildAgentTimingMetric(suffix, time);
            TrySend(metric);
        }

        #region HttpError

        public void ReportSupportabilityCollectorErrorException(string endpointMethod, TimeSpan responseDuration, HttpStatusCode? statusCode)
        {
            if (statusCode.HasValue)
            {
                TrySend(_metricBuilder.TryBuildSupportabilityErrorHttpStatusCodeFromCollector(statusCode.Value));
            }

            TrySend(_metricBuilder.TryBuildSupportabilityEndpointMethodErrorDuration(endpointMethod, responseDuration));
        }

        #endregion

        #region Log Events and Metrics

        // Used to report a logging framework is being instrumented for log forwarding, it does not take into account if forwarding was enabled or not.
        private ConcurrentDictionary<string, bool> _loggingFrameworksReported = new ConcurrentDictionary<string, bool>();
        public void ReportLogForwardingFramework(string logFramework)
        {
            _loggingFrameworksReported.TryAdd(logFramework, false);
        }

        // Used to report that both log forwarding is enabled and that a logging framework is being instrumented for logforwarding.
        private ConcurrentDictionary<string, bool> _loggingForwardingEnabledWithFrameworksReported = new ConcurrentDictionary<string, bool>();
        public void ReportLogForwardingEnabledWithFramework(string logFramework)
        {
            _loggingForwardingEnabledWithFrameworksReported.TryAdd(logFramework, false);
        }

        public void ReportLoggingEventsEmpty(int count = 1)
        {
            ReportSupportabilityCountMetric(MetricNames.SupportabilityLoggingEventEmpty);
        }

        public void CollectLoggingMetrics()
        {
            var totalCount = 0;
            foreach (var logLinesCounter in _logLinesCountByLevel)
            {
                if (TryGetCount(logLinesCounter.Value, out var linesCount))
                {
                    totalCount += linesCount;
                    TrySend(_metricBuilder.TryBuildLoggingMetricsLinesCountBySeverityMetric(logLinesCounter.Key, linesCount));
                }
            }

            if (totalCount > 0)
            {
                TrySend(_metricBuilder.TryBuildLoggingMetricsLinesCountMetric(totalCount));
            }

            foreach (var kvp in _loggingFrameworksReported)
            {
                if (kvp.Value == false)
                {
                    ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogFrameworkName(kvp.Key));
                    _loggingFrameworksReported[kvp.Key] = true;
                }
            }

            foreach (var kvp in _loggingForwardingEnabledWithFrameworksReported)
            {
                if (kvp.Value == false)
                {
                    ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogForwardingEnabledWithFrameworkName(kvp.Key));
                    _loggingForwardingEnabledWithFrameworksReported[kvp.Key] = true;
                }
            }

            var totalDeniedCount = 0;
            foreach (var logLinesDeniedCounter in _logDeniedCountByLevel)
            {
                if (TryGetCount(logLinesDeniedCounter.Value, out var linesCount))
                {
                    totalDeniedCount += linesCount;
                    TrySend(_metricBuilder.TryBuildLoggingMetricsDeniedCountBySeverityMetric(logLinesDeniedCounter.Key, linesCount));
                }
            }

            if (totalDeniedCount > 0)
            {
                TrySend(_metricBuilder.TryBuildLoggingMetricsDeniedCountMetric(totalDeniedCount));
            }

        }

        public void IncrementLogLinesCount(string level)
        {
            _logLinesCountByLevel.TryAdd(level, new InterlockedCounter());
            _logLinesCountByLevel[level].Increment();
        }

        public void IncrementLogDeniedCount(string level)
        {
            _logDeniedCountByLevel.TryAdd(level, new InterlockedCounter());
            _logDeniedCountByLevel[level].Increment();
        }

        public void ReportLoggingEventCollected() => TrySend(_metricBuilder.TryBuildSupportabilityLoggingEventsCollectedMetric());

        public void ReportLoggingEventsSent(int count)
        {
            TrySend(_metricBuilder.TryBuildSupportabilityLoggingEventsSentMetric(count));
            _agentHealthEventCounters[AgentHealthEvent.Log]?.Add(count);
        }

        public void ReportLoggingEventsDropped(int droppedCount) => TrySend(_metricBuilder.TryBuildSupportabilityLoggingEventsDroppedMetric(droppedCount));

        public void ReportIfAppDomainCachingDisabled()
        {
            if (_configuration.AppDomainCachingDisabled)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityAppDomainCachingDisabled);
            }
        }

        public void ReportLogForwardingConfiguredValues()
        {
            ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogMetricsConfiguredName(_configuration.LogMetricsCollectorEnabled));
            ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogForwardingConfiguredName(_configuration.LogEventCollectorEnabled));
            ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogDecoratingConfiguredName(_configuration.LogDecoratorEnabled));
            ReportSupportabilityCountMetric(MetricNames.GetSupportabilityLogLabelsConfiguredName(_configuration.LabelsEnabled));
        }

        #endregion

        #region Agent Control

        private void ReportIfAgentControlHealthEnabled()
        {
            if (_configuration.AgentControlEnabled)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityAgentControlHealthEnabled);
            }
        }

        public void SetAgentControlStatus((bool IsHealthy, string Code, string Status) healthStatus, params string[] statusParams)
        {

            // Do nothing if agent control is not enabled
            if (!_configuration.AgentControlEnabled)
                return;

            if (healthStatus.Equals(HealthCodes.AgentShutdownHealthy))
            {
                if (_healthCheck.IsHealthy)
                {
                    _healthCheck.TrySetHealth(healthStatus);
                }
            }
            else
            {
                _healthCheck.TrySetHealth(healthStatus, statusParams);
            }
        }

        public void PublishAgentControlHealthCheck()
        {
            if (!_healthChecksInitialized) // initialize on first invocation
            {
                InitializeHealthChecks();
                _healthChecksInitialized = true;
            }

            // stop the scheduled task if agent control isn't enabled or health checks fail for any reason
            if (!_configuration.AgentControlEnabled || _healthChecksFailed)
            {
                _scheduler.StopExecuting(PublishAgentControlHealthCheck);
                return;
            }

            var healthCheckYaml = _healthCheck.ToYaml();

            Log.Finest("Publishing Agent Control health check report: {HealthCheckYaml}", healthCheckYaml);

            try
            {
                using var fs = _fileWrapper.OpenWrite(Path.Combine(_healthCheckPath, _healthCheck.FileName));
                var payloadBytes = Encoding.UTF8.GetBytes(healthCheckYaml);
                fs.Write(payloadBytes, 0, payloadBytes.Length);
                fs.Flush();
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Failed to write Agent Control health check report. Health checks will be disabled.");
                _healthChecksFailed = true;
            }
        }

        private void InitializeHealthChecks()
        {
            if (!_configuration.AgentControlEnabled)
            {
                Log.Debug("Agent Control is disabled. Health checks will not be reported.");
                return;
            }

            Log.Debug("Initializing Agent Control health checks");

            // make sure the delivery location is a file URI
            var fileUri = new Uri(_configuration.HealthDeliveryLocation);
            if (fileUri.Scheme != Uri.UriSchemeFile)
            {
                Log.Warn("Agent Control is enabled but the provided agent_control.health.delivery_location is not a file URL. Health checks will be disabled.");
                _healthChecksFailed = true;
                return;
            }

            _healthCheckPath = fileUri.LocalPath;

            // verify the directory exists
            if (!_directoryWrapper.Exists(_healthCheckPath))
            {
                Log.Warn("Agent Control is enabled but the path specified in agent_control.health.delivery_location does not exist. Health checks will be disabled.");
                _healthChecksFailed = true;
            }

            // verify we can write a file to the directory
            var testFile = Path.Combine(_healthCheckPath, Path.GetRandomFileName());
            if (!_fileWrapper.TryCreateFile(testFile))
            {
                Log.Warn("Agent Control is enabled but the agent is unable to create files in the directory specified in agent_control.health.delivery_location. Health checks will be disabled.");
                _healthChecksFailed = true;
            }
        }
        #endregion

        public void ReportSupportabilityPayloadsDroppeDueToMaxPayloadSizeLimit(string endpoint)
        {
            TrySend(_metricBuilder.TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit(endpoint));
        }

        // Only one metric harvest happens at a time, so locking around this bool is not important
        private bool _oneTimeMetricsCollected;
        private void CollectOneTimeMetrics()
        {
            if (_oneTimeMetricsCollected) return;
            _oneTimeMetricsCollected = true;

            ReportLogForwardingConfiguredValues();
            ReportIfAppDomainCachingDisabled();
            ReportInfiniteTracingOneTimeMetrics();
            ReportIfLoggingDisabled();
            ReportIfInstrumentationIsDisabled();
            ReportIfGCSamplerV2IsEnabled();
            ReportIfAwsAccountIdProvided();
            ReportIfAgentControlHealthEnabled();
            ReportIfAspNetCore6PlusIsEnabled();
            ReportIfAzureFunctionModeIsDetected();
        }

        public void CollectMetrics()
        {
            CollectDistributedTraceMetrics();
            CollectTraceContextSuccessMetrics();
            CollectInfiniteTracingMetrics();
            CollectLoggingMetrics();
            CollectSupportabilityDataUsageMetrics();

            ReportAgentVersion(AgentInstallConfiguration.AgentVersion);
            ReportIfHostIsLinuxOs();
            ReportDotnetVersion();
            ReportAgentInfo();

            CollectOneTimeMetrics();

            ReportCustomInstrumentationCountMetrics();
        }

        private void ReportCustomInstrumentationCountMetrics()
        {
            TrySend(_metricBuilder.TryBuildCountMetric(MetricNames.SupportabilityCustomInstrumentationCount, _customInstrumentationCounter.Value));
        }

        public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
        {
            if (_publishMetricDelegate != null)
            {
                Log.Warn("Existing PublishMetricDelegate registration being overwritten.");
            }

            _publishMetricDelegate = publishMetricDelegate;
        }

        private void TrySend(MetricWireModel metric)
        {
            if (metric == null)
            {
                return;
            }

            if (_publishMetricDelegate == null)
            {
                Log.Warn("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricNameModel.Name);
                return;
            }

            try
            {
                _publishMetricDelegate(metric);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TrySend() failed");
            }
        }
        private bool TryGetCount(InterlockedCounter counter, out int metricCount)
        {
            metricCount = 0;
            if (counter.Value > 0)
            {
                metricCount = counter.Exchange(0);
                return true;
            }

            return false;
        }

        private bool TryGetCount(InterlockedLongCounter counter, out long metricCount)
        {
            metricCount = 0;
            if (counter.Value > 0)
            {
                metricCount = counter.Exchange(0);
                return true;
            }

            return false;
        }

        private ConcurrentBag<DestinationInteractionSample> _externalApiDataUsageSamples = new ConcurrentBag<DestinationInteractionSample>();


        public void ReportSupportabilityDataUsage(string api, string apiArea, long dataSent, long dataReceived)
        {
            _externalApiDataUsageSamples.Add(new DestinationInteractionSample(api, apiArea, dataSent, dataReceived));
        }

        private void CollectSupportabilityDataUsageMetrics()
        {
            var currentHarvest = Interlocked.Exchange(ref _externalApiDataUsageSamples, new ConcurrentBag<DestinationInteractionSample>());

            foreach (var destination in currentHarvest.GroupBy(x => x.Api))
            {
                // Setup top level metrics to aggregate
                var destinationName = string.IsNullOrWhiteSpace(destination.Key) ? "UnspecifiedDestination" : destination.Key;
                var destinationCallCount = 0L;
                var destinationBytesSent = 0L;
                var destinationBytesReceived = 0L;

                // inspect and report sub-metrics
                foreach (var destinationArea in destination.GroupBy(x => x.ApiArea))
                {
                    var destinationAreaName = string.IsNullOrWhiteSpace(destinationArea.Key) ? "UnspecifiedDestinationArea" : destinationArea.Key;
                    var destinationAreaCallCount = 0L;
                    var destinationAreaBytesSent = 0L;
                    var destinationAreaBytesReceived = 0L;

                    // accumulate values for this destination sub-area 
                    foreach (var dataSample in destinationArea)
                    {
                        destinationAreaCallCount++;
                        destinationAreaBytesSent += dataSample.BytesSent;
                        destinationAreaBytesReceived += dataSample.BytesReceived;
                    }

                    // increment top level metrics
                    destinationCallCount += destinationAreaCallCount;
                    destinationBytesSent += destinationAreaBytesSent;
                    destinationBytesReceived += destinationAreaBytesReceived;

                    ReportSupportabilityDataUsageMetric(
                        MetricNames.GetPerDestinationAreaDataUsageMetricName(destinationName, destinationAreaName),
                        destinationAreaCallCount,
                        destinationAreaBytesSent,
                        destinationAreaBytesReceived);
                }

                ReportSupportabilityDataUsageMetric(
                    MetricNames.GetPerDestinationDataUsageMetricName(destinationName),
                    destinationCallCount,
                    destinationBytesSent,
                    destinationBytesReceived);
            }
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // Some one time metrics are reporting configured values, so we want to re-report them if the configuration changed
            _oneTimeMetricsCollected = false;
        }

        private void ReportIfLoggingDisabled()
        {
            if (!_configuration.LoggingEnabled)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityLoggingDisabled);
            }
            if (Log.FileLoggingHasFailed)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityLoggingFatalError);
            }
        }

        private void ReportIfInstrumentationIsDisabled()
        {
            var ignoredCount = _configuration.IgnoredInstrumentation.Count();
            if (ignoredCount > 0)
            {
                ReportSupportabilityGaugeMetric(MetricNames.SupportabilityIgnoredInstrumentation, ignoredCount);
            }
        }

        private void ReportIfGCSamplerV2IsEnabled()
        {
            if (_configuration.GCSamplerV2Enabled)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityGCSamplerV2Enabled);
            }
        }

        private void ReportIfAwsAccountIdProvided()
        {
            if (!string.IsNullOrEmpty(_configuration.AwsAccountId))
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityAwsAccountIdProvided);
            }
        }

        private void ReportIfAzureFunctionModeIsDetected()
        {
            if (_configuration.AzureFunctionModeDetected)
            {
                ReportSupportabilityCountMetric(MetricNames.SupportabilityAzureFunctionMode(_configuration.AzureFunctionModeEnabled));
            }
        }

        private void ReportIfAspNetCore6PlusIsEnabled()
        {
            ReportSupportabilityCountMetric(MetricNames.SupportabilityAspNetCore6PlusBrowserInjection(_configuration.EnableAspNetCore6PlusBrowserInjection));
        }

        /// <summary>
        /// FOR UNIT TESTING ONLY
        /// </summary>
        public bool HealthCheckFailed => _healthChecksFailed;
    }
}
