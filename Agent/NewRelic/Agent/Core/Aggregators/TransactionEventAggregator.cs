using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface ITransactionEventAggregator
	{
		void Collect(TransactionEventWireModel transactionEventWireModel);
		IAdaptiveSampler AdaptiveSampler { get; }
	}

	/// <summary>
	/// An service for collecting and managing transaction events.
	/// </summary>
	public class TransactionEventAggregator : AbstractAggregator<TransactionEventWireModel>, ITransactionEventAggregator
	{
		private readonly IAgentHealthReporter _agentHealthReporter;

		// Note that synthetics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
		private IResizableCappedCollection<PrioritizedNode<TransactionEventWireModel>> _transactionEvents = new ConcurrentPriorityQueue<PrioritizedNode< TransactionEventWireModel>>(0);

		private ConcurrentList<TransactionEventWireModel> _syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();

		private const double ReservoirReductionSizeMultiplier = 0.5;

		public IAdaptiveSampler AdaptiveSampler { get; }

		public TransactionEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter, IAdaptiveSampler adaptiveSampler)
			: base(dataTransportService, scheduler, processStatic)
		{
			_agentHealthReporter = agentHealthReporter;
			AdaptiveSampler = adaptiveSampler;
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
			var transactionEvents = _transactionEvents.Where(node => node != null).Select(node => node.Data).ToList();
			var syntheticsTransactionEvents = _syntheticsTransactionEvents;
			var aggregatedEvents = transactionEvents.Union(syntheticsTransactionEvents).ToList();

			// EventHarvestData is required for extrapolation in the UI.
			var eventHarvestData = new EventHarvestData(_transactionEvents.Size, (uint)_transactionEvents.GetAddAttemptsCount());

			ResetCollections(GetReservoirSize());

			// if we don't have any events to publish then don't
			if (aggregatedEvents.Count <= 0)
				return;

			var responseStatus = DataTransportService.Send(eventHarvestData, aggregatedEvents);

			AdaptiveSampler.EndOfSamplingInterval();

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
			_transactionEvents = new ConcurrentPriorityQueue<PrioritizedNode<TransactionEventWireModel>>(transactionEventCollectionCapacity);
			_syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();
		}

		private void AddEventToCollection(TransactionEventWireModel transactionEvent)
		{
			if (transactionEvent.IsSynthetics() && _syntheticsTransactionEvents.Count < SyntheticsHeader.MaxEventCount)
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
					ReduceReservoirSize((uint)(transactionEvents.Count * ReservoirReductionSizeMultiplier));
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

			foreach(var transactionEvent in transactionEvents)
			{
				if (transactionEvent != null)
				{
					AddEventToCollection(transactionEvent);
				}
			}
		}

		private uint GetReservoirSize()
		{
			return _transactionEvents.Size;
		}

		private void ReduceReservoirSize(uint newSize)
		{
			if (newSize >= GetReservoirSize())
				return;

			_transactionEvents.Resize(newSize);
			_agentHealthReporter.ReportTransactionEventReservoirResized(newSize);
		}
	}
}