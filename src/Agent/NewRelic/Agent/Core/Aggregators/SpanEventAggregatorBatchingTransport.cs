// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Collections;
using NewRelic.Agent.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ISpanEventAggregatorBatchingTransport
    {
        void Collect(ISpanEventWireModel wireModel);
        void Collect(IEnumerable<ISpanEventWireModel> wireModels);
        bool IsServiceEnabled { get; }
        void RecordDroppedSpans(int countDroppedSpans);
        void RecordSeenSpans(int countSeenSpans);
        void ReportSupportabilityMetrics();
        int Capacity { get; }
    }

    public class SpanEventAggregatorBatchingTransport : DisposableService, ISpanEventAggregatorBatchingTransport
    {
        private PartitionedBlockingCollection<Span> _spanEvents;
        private readonly IBatchTransportService<Span> _spanBatchingService;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IConfigurationService _configSvc;
        private readonly IScheduler _schedulerSvc;

        private IConfiguration _configuration => _configSvc?.Configuration;

        // TODO: Use dedicated configuration settings
        public int Capacity => (_spanEvents?.Capacity).GetValueOrDefault(0);

        public SpanEventAggregatorBatchingTransport(IBatchTransportService<Span> spanBatchingService, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter, IScheduler scheduler)
        {   
            _spanBatchingService = spanBatchingService;
            _subscriptions.Add<AgentConnectedEvent>(AgentConnected);
            _subscriptions.Add<PreCleanShutdownEvent>(OnPreCleanShutdown);
            _agentHealthReporter = agentHealthReporter;
            _configSvc = configSvc;
            _schedulerSvc = scheduler;
        }

        /// <summary>
        /// This executes every time a local configuration change is made.  It is more convenient
        /// than OnConfigurationChanged
        /// </summary>
        /// <param name="_"></param>
        private void AgentConnected(AgentConnectedEvent _)
        {
            _spanBatchingService.Shutdown(false);
            _schedulerSvc.StopExecuting(ReportSupportabilityMetrics);

            var oldCapacity = (_spanEvents?.Capacity).GetValueOrDefault(0);
            var oldPartitionCount = (_spanEvents?.PartitionCount).GetValueOrDefault(0);
            var oldCount = (_spanEvents?.Count).GetValueOrDefault(0);

            var oldCollection = _spanEvents != null
                ? Interlocked.Exchange(ref _spanEvents, null)
                : null;

            // TODO: Stop referencing infinite tracing settings and use dedicated configuration settings
            var newCapacity = _configuration.InfiniteTracingQueueSizeSpans;
            var newPartitionCount = Math.Min(_configuration.InfiniteTracingQueueSizeSpans, _configuration.InfiniteTracingPartitionCountSpans);

            if (!IsServiceEnabled || newCapacity <= 0 || newPartitionCount <= 0 || newPartitionCount > 62)
            {
                if (oldCount > 0)
                {
                    RecordDroppedSpans(oldCount);
                }

                if(IsServiceEnabled)
                {
                    // TODO: Stop referencing infinite tracing
                    Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration is invalid - Infinite Tracing will NOT be enabled.");
                    LogConfiguration();
                }

                return;
            }

            if (oldCapacity == newCapacity && oldPartitionCount == newPartitionCount && oldCollection != null)
            {
                _spanEvents = oldCollection;
            }
            else
            {
                var overflowCount = oldCount - newCapacity;
                if (overflowCount > 0)
                {
                    RecordDroppedSpans(overflowCount);
                }

                _spanEvents = oldCollection != null
                    ? new PartitionedBlockingCollection<Span>(newCapacity, newPartitionCount, oldCollection)
                    : new PartitionedBlockingCollection<Span>(newCapacity, newPartitionCount);
            }

            LogConfiguration();

            _schedulerSvc.ExecuteEvery(ReportSupportabilityMetrics, TimeSpan.FromMinutes(1));
            _spanBatchingService.StartConsumingCollection(_spanEvents);
        }

        private void OnPreCleanShutdown(PreCleanShutdownEvent obj)
        {
            // TODO: Stop referencing infinite tracing settings and use dedicated configuration settings
            if (_configuration.CollectorSendDataOnExit)
            {
                _spanBatchingService.Wait(_configuration.InfiniteTracingExitTimeoutMs);
            }

            return;
        }

        public void ReportSupportabilityMetrics()
        {
            _agentHealthReporter.ReportInfiniteTracingSpanQueueSize(_spanEvents.Count);
        }

        private void LogConfiguration()
        {
            // TODO: Use dedicated configuration settings instead of infinite tracing settings
            Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Size - {_configuration.InfiniteTracingQueueSizeSpans}");
            Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Partitions - {_configuration.InfiniteTracingPartitionCountSpans}");
        }

        public bool IsServiceEnabled => _configuration.SpanEventsEnabled
            && _configuration.DistributedTracingEnabled
            && _spanBatchingService.IsServiceEnabled;

        public void RecordDroppedSpans(int countDroppedSpans)
        {
            // TODO: Record correct supportability metric for dropped spans
            _agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(countDroppedSpans);
        }

        public void RecordSeenSpans(int countSeenSpans)
        {
            // TODO: Record correct supportability metric for seen spans
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(countSeenSpans);
        }

        public void Collect(ISpanEventWireModel wireModel)
        {
            RecordSeenSpans(1);

            if (_spanEvents == null || !_spanEvents.TryAdd(wireModel.Span))
            {
                RecordDroppedSpans(1);
            }
        }

        public void Collect(IEnumerable<ISpanEventWireModel> wireModels)
        {
            if (_spanEvents == null)
            {
                var countSpans = wireModels.Count();

                _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(countSpans);
                RecordDroppedSpans(countSpans);

                return;
            }

            foreach (var span in wireModels)
            {
                Collect(span);
            }
        }
    }
}
