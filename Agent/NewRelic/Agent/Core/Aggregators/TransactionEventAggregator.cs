using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Core.DistributedTracing;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface ITransactionEventAggregator
	{
		void Collect([NotNull] TransactionEventWireModel transactionEventWireModel);
		IAdaptiveSampler AdaptiveSampler { get; }
	}

	/// <summary>
	/// An service for collecting and managing transaction events.
	/// </summary>
	public class TransactionEventAggregator : AbstractAggregator<TransactionEventWireModel>, ITransactionEventAggregator
	{
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		// Note that synethics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
		[NotNull]
		private IResizableCappedCollection<TransactionEventWireModel> _transactionEvents = new ConcurrentPriorityQueue<TransactionEventWireModel>(0, new TransactionEventWireModel.PriorityTimestampComparer());

		[NotNull]
		private ConcurrentList<TransactionEventWireModel> _syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();

		[NotNull]
		private const Double ReservoirReductionSizeMultiplier = 0.5;

		[NotNull]
		public IAdaptiveSampler AdaptiveSampler { get; }

		public TransactionEventAggregator([NotNull] IDataTransportService dataTransportService, [NotNull] IScheduler scheduler, [NotNull] IProcessStatic processStatic, [NotNull] IAgentHealthReporter agentHealthReporter, IAdaptiveSampler adaptiveSampler)
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
			var transactionEvents = _transactionEvents;
			var syntheticsTransactionEvents = _syntheticsTransactionEvents;
			var aggregatedEvents = transactionEvents.Union(syntheticsTransactionEvents).ToList();

			ResetCollections(GetReservoirSize());

			// if we don't have any events to publish then don't
			if (aggregatedEvents.Count <= 0)
				return;

			_agentHealthReporter.ReportTransactionEventsSent(aggregatedEvents.Count);
			var responseStatus = DataTransportService.Send(aggregatedEvents);

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
			_transactionEvents = new ConcurrentPriorityQueue<TransactionEventWireModel>(transactionEventCollectionCapacity, new TransactionEventWireModel.PriorityTimestampComparer());
			_syntheticsTransactionEvents = new ConcurrentList<TransactionEventWireModel>();
		}

		private void AddEventToCollection([NotNull] TransactionEventWireModel transactionEvent)
		{
			if (transactionEvent.IsSynthetics() && _syntheticsTransactionEvents.Count < SyntheticsHeader.MaxEventCount)
			{
				_syntheticsTransactionEvents.Add(transactionEvent);
			}
			else
			{
				_transactionEvents.Add(transactionEvent);
			}
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, [NotNull] IEnumerable<TransactionEventWireModel> transactionEvents)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.CommunicationError:
				case DataTransportResponseStatus.RequestTimeout:
				case DataTransportResponseStatus.ServerError:
				case DataTransportResponseStatus.ConnectionError:
					RetainEvents(transactionEvents);
					break;
				case DataTransportResponseStatus.PostTooBigError:
					ReduceReservoirSize((UInt32)(transactionEvents.Count() * ReservoirReductionSizeMultiplier));
					RetainEvents(transactionEvents);
					break;
				case DataTransportResponseStatus.OtherError:
				case DataTransportResponseStatus.RequestSuccessful:
				default:
					break;
			}
		}

		private void RetainEvents([NotNull] IEnumerable<TransactionEventWireModel> transactionEvents)
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