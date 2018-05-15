using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
	public struct ErrorEventAdditions
	{
		public uint reservoir_size;
		public uint events_seen;
	}

	public interface IErrorEventAggregator
	{
		void Collect([NotNull] ErrorEventWireModel errorEventWireModel);
	}

	/// <summary>
	/// An service for collecting and managing error events.
	/// </summary>
	public class ErrorEventAggregator : AbstractAggregator<ErrorEventWireModel>, IErrorEventAggregator
	{
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		[NotNull]
		private IResizableCappedCollection<ErrorEventWireModel> _errorEvents = new ConcurrentPriorityQueue<ErrorEventWireModel>(0, new ErrorEventWireModel.PriorityTimestampComparer());

		// Note that Synthetics events must be recorded, and thus are stored in their own unique reservoir to ensure that they
		// are never pushed out by non-Synthetics events.
		[NotNull]
		private ConcurrentList<ErrorEventWireModel> _syntheticsErrorEvents = new ConcurrentList<ErrorEventWireModel>();

		[NotNull]
		private const Double ReservoirReductionSizeMultiplier = 0.5;

		public ErrorEventAggregator([NotNull] IDataTransportService dataTransportService, [NotNull] IScheduler scheduler, [NotNull] IProcessStatic processStatic, [NotNull] IAgentHealthReporter agentHealthReporter)
			: base(dataTransportService, scheduler, processStatic)
		{
			_agentHealthReporter = agentHealthReporter;
			ResetCollections(_configuration.ErrorCollectorMaxEventSamplesStored);
		}

		public override void Collect(ErrorEventWireModel errorEventWireModel)
		{
			_agentHealthReporter.ReportErrorEventSeen();

			AddEventToCollection(errorEventWireModel);
		}

		protected override void Harvest()
		{
			// create new reservoirs to put future events into (we don't want to add events to a reservoir that is being sent)
			var errorEvents = _errorEvents;
			var syntheticErrorEvents = _syntheticsErrorEvents;
			var aggregatedEvents = errorEvents.Union(syntheticErrorEvents).ToList();

			// Retrieve the number of add attempts before resetting the collection.
			var addAttempts = _errorEvents.GetAddAttemptsCount();

			ResetCollections(GetReservoirSize());

			// if we don't have any events to publish then don't
			if (aggregatedEvents.Count <= 0)
				return;

			_agentHealthReporter.ReportErrorEventsSent(aggregatedEvents.Count);
			 
			var additions = new ErrorEventAdditions();
			additions.reservoir_size = GetReservoirSize();
			additions.events_seen = Convert.ToUInt32(addAttempts);

			var responseStatus = DataTransportService.Send(additions, aggregatedEvents);

			HandleResponse(responseStatus, aggregatedEvents);
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			ResetCollections(_configuration.ErrorCollectorMaxEventSamplesStored); 
		}

		#region Private Helpers

		private void ResetCollections(uint errorEventCollectionCapacity)
		{
			_errorEvents = new ConcurrentPriorityQueue<ErrorEventWireModel>(errorEventCollectionCapacity, new ErrorEventWireModel.PriorityTimestampComparer());
			_syntheticsErrorEvents = new ConcurrentList<ErrorEventWireModel>();

		}

		private void AddEventToCollection([NotNull] ErrorEventWireModel errorEvents)
		{
			if (errorEvents.IsSynthetics() && _syntheticsErrorEvents.Count < SyntheticsHeader.MaxEventCount)
				_syntheticsErrorEvents.Add(errorEvents);
			else
				_errorEvents.Add(errorEvents);
		}

		private UInt32 GetReservoirSize()
		{
			return _errorEvents.Size;
		}

		private void ReduceReservoirSize(UInt32 newSize)
		{
			if (newSize >= GetReservoirSize())
				return;

			_errorEvents.Resize(newSize);
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, [NotNull] IEnumerable<ErrorEventWireModel> errorEvents)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.ServiceUnavailableError:
				case DataTransportResponseStatus.ConnectionError:
					RetainEvents(errorEvents);
					break;
				case DataTransportResponseStatus.PostTooBigError:
					ReduceReservoirSize((UInt32)(errorEvents.Count() * ReservoirReductionSizeMultiplier));
					RetainEvents(errorEvents);
					break;
				case DataTransportResponseStatus.OtherError:
				case DataTransportResponseStatus.RequestSuccessful:
				default:
					break;
			}
		}

		private void RetainEvents([NotNull] IEnumerable<ErrorEventWireModel> errorEvents)
		{
			errorEvents = errorEvents.ToList();

			foreach(var errorEvent in errorEvents)
			{
				if ( errorEvent != null)
				{
					AddEventToCollection(errorEvent);
				}
			}
		}

		#endregion
	}
}
