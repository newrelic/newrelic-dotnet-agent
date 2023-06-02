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
    public interface ITransactionEventAggregator
    {
        void Collect(TransactionEventWireModel transactionEventWireModel);
    }

    /// <summary>
    /// An service for collecting and managing transaction events.
    /// </summary>
    public class TransactionEventAggregator : AbstractAggregator<TransactionEventWireModel>, ITransactionEventAggregator
    {
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly ReaderWriterLockSlim _readerWriterLock = new ReaderWriterLockSlim();

        // Note that synthetics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
        private IResizableCappedCollection<PrioritizedNode<TransactionEventWireModel>> _transactionEvents = new ConcurrentPriorityQueue<PrioritizedNode<TransactionEventWireModel>>(0);

        private ConcurrentList<TransactionEventWireModel> _syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();

        private const double ReservoirReductionSizeMultiplier = 0.5;

        public TransactionEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            ResetCollections(_configuration.TransactionEventsMaximumSamplesStored);
        }

        protected override TimeSpan HarvestCycle => _configuration.TransactionEventsHarvestCycle;
        protected override bool IsEnabled => _configuration.TransactionEventsEnabled;

        public override void Dispose()
        {
            base.Dispose();
            _readerWriterLock.Dispose();
        }

        public override void Collect(TransactionEventWireModel transactionEventWireModel)
        {
            _agentHealthReporter.ReportTransactionEventCollected();

            _readerWriterLock.EnterReadLock();
            try
            {
                AddEventToCollection(transactionEventWireModel);
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }
        }

        protected override async Task HarvestAsync()
        {
            Log.Finest("Transaction Event harvest starting.");

            IResizableCappedCollection<PrioritizedNode<TransactionEventWireModel>> originalTransactionEvents;
            ConcurrentList<TransactionEventWireModel> originalSyntheticsTransactionEvents;
            _readerWriterLock.EnterWriteLock();
            try
            {
                originalTransactionEvents = GetAndResetRegularTransactionEvents(_transactionEvents.Size);
                originalSyntheticsTransactionEvents = GetAndResetSyntheticsTransactionEvents();
            }
            finally
            {
                _readerWriterLock.ExitWriteLock();
            }

            var transactionEvents = originalTransactionEvents.Where(node => node != null).Select(node => node.Data).ToList();
            var aggregatedEvents = transactionEvents.Union(originalSyntheticsTransactionEvents).ToList();

            // EventHarvestData is required for extrapolation in the UI.
            var eventHarvestData = new EventHarvestData(originalTransactionEvents.Size, originalTransactionEvents.GetAddAttemptsCount());

            // if we don't have any events to publish then don't
            if (aggregatedEvents.Count <= 0)
                return;

            var responseStatus = await DataTransportService.SendAsync(eventHarvestData, aggregatedEvents);

            HandleResponse(responseStatus, aggregatedEvents);

            Log.Finest("Transaction Event harvest finished.");
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            ResetCollections(_configuration.TransactionEventsMaximumSamplesStored);
        }

        private void ResetCollections(int transactionEventCollectionCapacity)
        {
            GetAndResetRegularTransactionEvents(transactionEventCollectionCapacity);
            GetAndResetSyntheticsTransactionEvents();
        }

        private IResizableCappedCollection<PrioritizedNode<TransactionEventWireModel>> GetAndResetRegularTransactionEvents(int transactionEventCollectionCapacity)
        {
            return Interlocked.Exchange(ref _transactionEvents, new ConcurrentPriorityQueue<PrioritizedNode<TransactionEventWireModel>>(transactionEventCollectionCapacity));
        }

        private ConcurrentList<TransactionEventWireModel> GetAndResetSyntheticsTransactionEvents()
        {
            return Interlocked.Exchange(ref _syntheticsTransactionEvents, new ConcurrentList<TransactionEventWireModel>());
        }

        private void AddEventToCollection(TransactionEventWireModel transactionEvent)
        {
            if (transactionEvent.IsSynthetics && _syntheticsTransactionEvents.Count < SyntheticsHeader.MaxEventCount)
            {
                _syntheticsTransactionEvents.Add(transactionEvent);
            }
            else
            {
                _transactionEvents.Add(new PrioritizedNode<TransactionEventWireModel>(transactionEvent));
            }
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<TransactionEventWireModel> transactionEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.RequestSuccessful:
                    _agentHealthReporter.ReportTransactionEventsSent(transactionEvents.Count);
                    break;
                case DataTransportResponseStatus.Retain:
                    RetainEvents(transactionEvents);
                    break;
                case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
                    ReduceReservoirSize((int)(transactionEvents.Count * ReservoirReductionSizeMultiplier));
                    RetainEvents(transactionEvents);
                    break;
                case DataTransportResponseStatus.Discard:
                default:
                    break;
            }
        }

        private void RetainEvents(IEnumerable<TransactionEventWireModel> transactionEvents)
        {
            transactionEvents = transactionEvents.ToList();
            _agentHealthReporter.ReportTransactionEventsRecollected(transactionEvents.Count());

            foreach (var transactionEvent in transactionEvents)
            {
                if (transactionEvent != null)
                {
                    AddEventToCollection(transactionEvent);
                }
            }
        }

        private int GetReservoirSize()
        {
            return _transactionEvents.Size;
        }

        private void ReduceReservoirSize(int newSize)
        {
            if (newSize >= GetReservoirSize())
                return;

            _transactionEvents.Resize(newSize);
            _agentHealthReporter.ReportTransactionEventReservoirResized(newSize);
        }
    }
}
