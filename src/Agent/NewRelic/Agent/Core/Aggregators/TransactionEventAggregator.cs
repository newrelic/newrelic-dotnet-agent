using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;

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

        // Note that synethics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
        private IResizableCappedCollection<TransactionEventWireModel> _transactionEvents = new ConcurrentReservoir<TransactionEventWireModel>(0);
        private ConcurrentList<TransactionEventWireModel> _syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();
        private const Double _reservoirReductionSizeMultiplier = 0.5;

        public TransactionEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            ResetCollections(_configuration.TransactionEventsMaxSamplesStored);
        }

        public override void Collect(TransactionEventWireModel transactionEventWireModel)
        {
            _agentHealthReporter.ReportTransactionEventCollected();
            AddEventToCollection(transactionEventWireModel);
        }

        protected override void Harvest()
        {
            // create new reservoirs to put future events into (we don't want to add events to a reservoir that is being sent)
            var transactionEvents = _transactionEvents;
            var syntheticsTransactionEvents = _syntheticsTransactionEvents;
            var aggregatedEvents = transactionEvents.Union(syntheticsTransactionEvents).ToList();

            ResetCollections(GetReservoirSize());

            // if we don't have any events to publish then don't
            if (aggregatedEvents.Count <= 0)
                return;

            _agentHealthReporter.ReportTransactionEventsSent(aggregatedEvents.Count);
            var responseStatus = DataTransportService.Send(aggregatedEvents);

            HandleResponse(responseStatus, aggregatedEvents);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            ResetCollections(_configuration.TransactionEventsMaxSamplesStored);
        }

        private void ResetCollections(uint transactionEventCollectionCapacity)
        {
            _transactionEvents = new ConcurrentReservoir<TransactionEventWireModel>(transactionEventCollectionCapacity);
            _syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();
        }

        private void AddEventToCollection(TransactionEventWireModel transactionEvents)
        {
            if (transactionEvents.IsSynthetics() && _syntheticsTransactionEvents.Count < SyntheticsHeader.MaxEventCount)
                _syntheticsTransactionEvents.Add(transactionEvents);
            else
                _transactionEvents.Add(transactionEvents);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, IEnumerable<TransactionEventWireModel> transactionEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.ServiceUnavailableError:
                case DataTransportResponseStatus.ConnectionError:
                    RetainEvents(transactionEvents);
                    break;
                case DataTransportResponseStatus.PostTooBigError:
                    ReduceReservoirSize((UInt32)(transactionEvents.Count() * _reservoirReductionSizeMultiplier));
                    RetainEvents(transactionEvents);
                    break;
                case DataTransportResponseStatus.OtherError:
                case DataTransportResponseStatus.RequestSuccessful:
                default:
                    break;
            }
        }

        private void RetainEvents(IEnumerable<TransactionEventWireModel> transactionEvents)
        {
            transactionEvents = transactionEvents.ToList();
            _agentHealthReporter.ReportTransactionEventsRecollected(transactionEvents.Count());

            transactionEvents
                .Where(@event => @event != null)
                .ForEach(AddEventToCollection);
        }

        private UInt32 GetReservoirSize()
        {
            return _transactionEvents.Size;
        }

        private void ReduceReservoirSize(UInt32 newSize)
        {
            if (newSize >= GetReservoirSize())
                return;

            _transactionEvents.Resize(newSize);
            _agentHealthReporter.ReportTransactionEventReservoirResized(newSize);
        }
    }
}
