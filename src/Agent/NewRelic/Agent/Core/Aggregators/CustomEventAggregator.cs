// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Aggregators
{
    public interface ICustomEventAggregator
    {
        void Collect(CustomEventWireModel customEventWireModel);
    }

    /// <summary>
    /// An service for collecting and managing custom events.
    /// </summary>
    public class CustomEventAggregator : AbstractAggregator<CustomEventWireModel>, ICustomEventAggregator
    {
        private const double ReservoirReductionSizeMultiplier = 0.5;

        private readonly IAgentHealthReporter _agentHealthReporter;

        private readonly ReaderWriterLockSlim _readerWriterLockSlim = new ReaderWriterLockSlim();

        private ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>> _customEvents = new ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>>(0);

        public CustomEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            GetAndResetCollection(_configuration.CustomEventsMaximumSamplesStored);
        }

        public override void Dispose()
        {
            base.Dispose();
            _readerWriterLockSlim.Dispose();
        }

        protected override TimeSpan HarvestCycle => _configuration.CustomEventsHarvestCycle;
        protected override bool IsEnabled => _configuration.CustomEventsEnabled;

        public override void Collect(CustomEventWireModel customEventWireModel)
        {
            _agentHealthReporter.ReportCustomEventCollected();

            _readerWriterLockSlim.EnterReadLock();
            try
            {
                _customEvents.Add(new PrioritizedNode<CustomEventWireModel>(customEventWireModel));
            }
            finally
            {
                _readerWriterLockSlim.ExitReadLock();
            }
        }

        protected override async Task HarvestAsync()
        {
            Log.Finest("Custom Event harvest starting.");

            ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>> originalCustomEvents;

            _readerWriterLockSlim.EnterWriteLock();
            try
            {
                originalCustomEvents = GetAndResetCollection(_customEvents.Size);
            }
            finally
            {
                _readerWriterLockSlim.ExitWriteLock();
            }

            var customEvents = originalCustomEvents.Where(node => node != null).Select(node => node.Data).ToList();

            // if we don't have any events to publish then don't
            if (customEvents.Count <= 0)
                return;

            var responseStatus = await DataTransportService.SendAsync(customEvents);

            HandleResponse(responseStatus, customEvents);

            Log.Finest("Custom Event harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            GetAndResetCollection(_configuration.CustomEventsMaximumSamplesStored);
        }

        private ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>> GetAndResetCollection(int customEventCollectionCapacity)
        {
            return Interlocked.Exchange(ref _customEvents, new ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>>(customEventCollectionCapacity));
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<CustomEventWireModel> customEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportCustomEventsSent(customEvents.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(customEvents);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    var newSize = (int)(customEvents.Count * ReservoirReductionSizeMultiplier);
                    ReduceReservoirSize(newSize);
                    RetainEvents(customEvents);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }

        private void RetainEvents(IEnumerable<CustomEventWireModel> customEvents)
        {
            customEvents = customEvents.ToList();
            _agentHealthReporter.ReportCustomEventsRecollected(customEvents.Count());

            foreach (var customEvent in customEvents)
            {
                if (customEvent != null)
                {
                    _customEvents.Add(new PrioritizedNode<CustomEventWireModel>(customEvent));
                }
            }
        }

        private void ReduceReservoirSize(int newSize)
        {
            if (newSize >= _customEvents.Size)
                return;

            _customEvents.Resize(newSize);
            _agentHealthReporter.ReportCustomEventReservoirResized(newSize);
        }
    }
}
