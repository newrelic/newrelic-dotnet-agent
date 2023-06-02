// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
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
    public interface IErrorEventAggregator
    {
        void Collect(ErrorEventWireModel errorEventWireModel);
    }

    /// <summary>
    /// An service for collecting and managing error events.
    /// </summary>
    public class ErrorEventAggregator : AbstractAggregator<ErrorEventWireModel>, IErrorEventAggregator
    {
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        private ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>> _errorEvents = new ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>>(0);

        // Note that Synthetics events must be recorded, and thus are stored in their own unique reservoir to ensure that they
        // are never pushed out by non-Synthetics events.
        private ConcurrentList<ErrorEventWireModel> _syntheticsErrorEvents = new ConcurrentList<ErrorEventWireModel>();

        private const double ReservoirReductionSizeMultiplier = 0.5;

        public ErrorEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            ResetCollections(_configuration.ErrorCollectorMaxEventSamplesStored);
        }

        protected override TimeSpan HarvestCycle => _configuration.ErrorEventsHarvestCycle;
        protected override bool IsEnabled => _configuration.ErrorCollectorCaptureEvents;

        public override void Dispose()
        {
            base.Dispose();
            _readerWriterLock.Dispose();
        }

        public override void Collect(ErrorEventWireModel errorEventWireModel)
        {
            _agentHealthReporter.ReportErrorEventSeen();

            _readerWriterLock.EnterReadLock();
            try
            {
                AddEventToCollection(errorEventWireModel);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        protected override async Task HarvestAsync()
        {
            Log.Finest("Error Event harvest starting.");

            ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>> originalErrorEvents;
            ConcurrentList<ErrorEventWireModel> originalSyntheticsErrorEvents;

            _readerWriterLock.EnterWriteLock();
            try
            {
                originalErrorEvents = GetAndResetErrorEvents(GetReservoirSize());
                originalSyntheticsErrorEvents = GetAndResetSyntheticsErrorEvents();
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

            var errorEvents = originalErrorEvents.Where(node => node != null).Select(node => node.Data).ToList();
            var aggregatedEvents = errorEvents.Union(originalSyntheticsErrorEvents).ToList();

            // Retrieve the number of add attempts before resetting the collection.
            var eventHarvestData = new EventHarvestData(originalErrorEvents.Size, originalErrorEvents.GetAddAttemptsCount());

            // if we don't have any events to publish then don't
            if (aggregatedEvents.Count <= 0)
                return;

            var responseStatus = await DataTransportService.SendAsync(eventHarvestData, aggregatedEvents);

            HandleResponse(responseStatus, aggregatedEvents);

            Log.Finest("Error Event harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            ResetCollections(_configuration.ErrorCollectorMaxEventSamplesStored);
        }

        #region Private Helpers

        private void ResetCollections(int errorEventCollectionCapacity)
        {
            GetAndResetErrorEvents(errorEventCollectionCapacity);
            GetAndResetSyntheticsErrorEvents();
        }

        private ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>> GetAndResetErrorEvents(int errorEventCollectionCapacity)
        {
            return Interlocked.Exchange(ref _errorEvents, new ConcurrentPriorityQueue<PrioritizedNode<ErrorEventWireModel>>(errorEventCollectionCapacity));
        }

        private ConcurrentList<ErrorEventWireModel> GetAndResetSyntheticsErrorEvents()
        {
            return Interlocked.Exchange(ref _syntheticsErrorEvents, new ConcurrentList<ErrorEventWireModel>());
        }

        private void AddEventToCollection(ErrorEventWireModel errorEvent)
        {
            if (errorEvent.IsSynthetics && _syntheticsErrorEvents.Count < SyntheticsHeader.MaxEventCount)
            {
                _syntheticsErrorEvents.Add(errorEvent);
            }
            else
            {
                _errorEvents.Add(new PrioritizedNode<ErrorEventWireModel>(errorEvent));
            }
        }

        private int GetReservoirSize()
        {
            return _errorEvents.Size;
        }

        private void ReduceReservoirSize(int newSize)
        {
            if (newSize >= GetReservoirSize())
                return;

            _errorEvents.Resize(newSize);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<ErrorEventWireModel> errorEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportErrorEventsSent(errorEvents.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(errorEvents);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    ReduceReservoirSize((int)(errorEvents.Count * ReservoirReductionSizeMultiplier));
                    RetainEvents(errorEvents);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }

        private void RetainEvents(IEnumerable<ErrorEventWireModel> errorEvents)
        {
            errorEvents = errorEvents.ToList();

            foreach (var errorEvent in errorEvents)
            {
                if (errorEvent != null)
                {
                    AddEventToCollection(errorEvent);
                }
            }
        }

        #endregion
    }
}
