/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ISpanEventAggregatorInfiniteTracing
    {
        void Collect(ISpanEventWireModel wireModel);
        void Collect(IEnumerable<ISpanEventWireModel> wireModels);
        bool IsServiceEnabled { get; }
        bool IsServiceAvailable { get; }
        bool HasCapacity(int proposedItems);
        void RecordDroppedSpans(int countDroppedSpans);
    }

    public class SpanEventAggregatorInfiniteTracing : DisposableService, ISpanEventAggregatorInfiniteTracing
    {
        private BlockingCollection<Span> _spanEvents;
        private readonly IDataStreamingService<Span, RecordStatus> _spanStreamingService;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IConfigurationService _configSvc;
        private IConfiguration _configuration => _configSvc?.Configuration;

        public SpanEventAggregatorInfiniteTracing(IDataStreamingService<Span, RecordStatus> spanStreamingService, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter)
        {
            _spanStreamingService = spanStreamingService;
            _subscriptions.Add<AgentConnectedEvent>(AgentConnected);
            _agentHealthReporter = agentHealthReporter;
            _configSvc = configSvc;
        }

        /// <summary>
        /// This executes every time a local configuration change is made.  It is more convenient
        /// that OnConfigurationChanged
        /// </summary>
        /// <param name="_"></param>
        private void AgentConnected(AgentConnectedEvent _)
        {
            _spanStreamingService.Shutdown(false);
            var newCapacity = _configuration.InfiniteTracingQueueSizeSpans;

            if (!IsServiceEnabled || newCapacity <= 0)
            {
                if (_spanEvents != null)
                {
                    var oldqueue = Interlocked.Exchange(ref _spanEvents, null);
                    RecordDroppedSpans(oldqueue.Count);
                }
                return;
            }

            if (_spanEvents == null || newCapacity != _spanEvents.BoundedCapacity)
            {
                var oldCollection = _spanEvents;

                if (_spanEvents != null)
                {
                    Interlocked.Exchange(ref _spanEvents, new BlockingCollection<Span>(new ConcurrentQueue<Span>(_spanEvents.ToArray().Take(newCapacity)), newCapacity));
                }
                else
                {
                    _spanEvents = new BlockingCollection<Span>(newCapacity);
                }

                if (oldCollection != null && oldCollection.Count > newCapacity)
                {
                    var countDropped = oldCollection.Count - newCapacity;
                    RecordDroppedSpans(countDropped);
                }
            }


            LogConfiguration();

            _spanStreamingService.StartConsumingCollection(_spanEvents);
        }

        /// <summary>
        /// Allows the transformer to check to see if the aggregator has the capacity
        /// to store a proposed number of items.  If the streaming service has not been
        /// able to stream an item, set the capacity at 10% to avoid building up a large
        /// backlog of items during the time it takes to connect.
        /// </summary>
        /// <param name="proposedItems"></param>
        /// <returns></returns>
        public bool HasCapacity(int proposedItems)
        {
            var capacityFactor = _spanStreamingService.IsStreaming
                ? .9
                : .1;

            return _spanEvents != null && (_spanEvents.Count + proposedItems) < (_spanEvents.BoundedCapacity * capacityFactor);
        }

        private void LogConfiguration()
        {
            Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Size - {_configuration.InfiniteTracingQueueSizeSpans}");
        }

        public bool IsServiceEnabled => _spanStreamingService.IsServiceEnabled;
        public bool IsServiceAvailable => IsServiceEnabled && _spanEvents != null && _spanStreamingService.IsServiceAvailable;

        public void RecordDroppedSpans(int countDroppedSpans)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(countDroppedSpans);
        }

        public void Collect(ISpanEventWireModel wireModel)
        {
            _agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(1);

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
