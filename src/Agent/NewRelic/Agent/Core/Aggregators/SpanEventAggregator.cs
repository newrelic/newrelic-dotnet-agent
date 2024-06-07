// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Segments;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ISpanEventAggregator
    {
        void Collect(ISpanEventWireModel wireModel);
        void Collect(IEnumerable<ISpanEventWireModel> wireModels);
        bool IsServiceEnabled { get; }
        bool IsServiceAvailable { get; }
    }

    public class SpanEventAggregator : AbstractAggregator<ISpanEventWireModel>, ISpanEventAggregator
    {
        private const double ReservoirReductionSizeMultiplier = 0.5;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        private ConcurrentPriorityQueue<PrioritizedNode<ISpanEventWireModel>> _spanEvents = new ConcurrentPriorityQueue<PrioritizedNode<ISpanEventWireModel>>(0);

        protected override TimeSpan HarvestCycle => _configuration.SpanEventsHarvestCycle;

        protected override bool IsEnabled => _configuration.SpanEventsEnabled
            && _configuration.SpanEventsMaxSamplesStored > 0
            && _configuration.DistributedTracingEnabled;

        public bool IsServiceEnabled => IsEnabled;
        public bool IsServiceAvailable => IsEnabled;

        /// <summary>
        /// Atomically set a new ConcurrentPriorityQueue to _spanEvents and return the previous ConcurrentPriorityQueue reference;
        /// </summary>
        /// <returns>A reference to the previous ConcurrentPriorityQueue</returns>
        private ConcurrentPriorityQueue<PrioritizedNode<ISpanEventWireModel>> GetAndResetCollection()
        {
            return Interlocked.Exchange(ref _spanEvents, new ConcurrentPriorityQueue<PrioritizedNode<ISpanEventWireModel>>(_configuration.SpanEventsMaxSamplesStored));
        }

        private int AddWireModels(IEnumerable<ISpanEventWireModel> wireModels)
        {
            var nodes = wireModels.Where(model => null != model)
                .Select(model => new PrioritizedNode<ISpanEventWireModel>(model));
            return _spanEvents.Add(nodes);
        }

        public SpanEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            //we don't care about the returned CPQ because it was initialized with zero size.
            GetAndResetCollection();
        }

        public override void Dispose()
        {
            base.Dispose();
            _readerWriterLockSlim.Dispose();
        }

        public override void Collect(ISpanEventWireModel wireModel)
        {
            _agentHealthReporter.ReportSpanEventCollected(1);

            _readerWriterLockSlim.EnterReadLock();
            try
            {
                _spanEvents.Add(new PrioritizedNode<ISpanEventWireModel>(wireModel));
            }
            finally
            {
                _readerWriterLockSlim.ExitReadLock();
            }
        }

        public void Collect(IEnumerable<ISpanEventWireModel> wireModels)
        {
            int added;
            _readerWriterLockSlim.EnterReadLock();
            try
            {
                added = AddWireModels(wireModels);
            }
            finally
            {
                _readerWriterLockSlim.ExitReadLock();
            }
            _agentHealthReporter.ReportSpanEventCollected(added);
        }

        protected override void ManualHarvest(string transactionId) => InternalHarvest(transactionId);

        protected override void Harvest() => InternalHarvest();

        protected void InternalHarvest(string transactionId = null)
        {
            Log.Finest("Span Event harvest starting.");

            ConcurrentPriorityQueue<PrioritizedNode<ISpanEventWireModel>> spanEventsPriorityQueue;

            _readerWriterLockSlim.EnterWriteLock();
            try
            {
                spanEventsPriorityQueue = GetAndResetCollection();
            }
            finally
            {
                _readerWriterLockSlim.ExitWriteLock();
            }

            var eventHarvestData = new EventHarvestData(spanEventsPriorityQueue.Size, spanEventsPriorityQueue.GetAddAttemptsCount());
            var wireModels = spanEventsPriorityQueue.Where(node => null != node).Select(node => node.Data).ToList();
            
            // if we don't have any events to publish then don't
            if (wireModels.Count <= 0)
                return;

            var responseStatus = DataTransportService.Send(eventHarvestData, wireModels, transactionId);

            HandleResponse(responseStatus, wireModels);

            Log.Finest("Span Event harvest finished.");
        }

        private void ReduceReservoirSize(int newSize)
        {
            if (newSize >= _spanEvents.Size)
                return;

            _spanEvents.Resize(newSize);
        }

        private void RetainEvents(IEnumerable<ISpanEventWireModel> wireModels)
        {
            AddWireModels(wireModels);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<ISpanEventWireModel> spanEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportSpanEventsSent(spanEvents.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(spanEvents);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    ReduceReservoirSize((int)(spanEvents.Count * ReservoirReductionSizeMultiplier));
                    RetainEvents(spanEvents);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }
        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            if (configurationUpdateSource == ConfigurationUpdateSource.Local && _configuration.SpanEventsMaxSamplesStored != _spanEvents.Size)
            {
                //we might have received server configuration that says we should not have collected the
                //events that we have, drop them on the floor by ignoring the returned collection.
                GetAndResetCollection();
            }
        }
    }
}
