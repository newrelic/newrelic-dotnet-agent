using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface ICustomEventAggregator
	{
		void Collect([NotNull] CustomEventWireModel customEventWireModel);
	}

	/// <summary>
	/// An service for collecting and managing custom events.
	/// </summary>
	public class CustomEventAggregator : AbstractAggregator<CustomEventWireModel>, ICustomEventAggregator
	{
		[NotNull]
		private const Double ReservoirReductionSizeMultiplier = 0.5;

		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		// Note that synethics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
		private ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>> _customEvents = new ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>>(0);

		public CustomEventAggregator([NotNull] IDataTransportService dataTransportService, [NotNull] IScheduler scheduler, [NotNull] IProcessStatic processStatic, [NotNull] IAgentHealthReporter agentHealthReporter)
			: base(dataTransportService, scheduler, processStatic)
		{
			_agentHealthReporter = agentHealthReporter;
			ResetCollections(_configuration.CustomEventsMaxSamplesStored);
		}

		public override void Collect(CustomEventWireModel customEventWireModel)
		{
			_agentHealthReporter.ReportCustomEventCollected();
			_customEvents.Add(new PrioritizedNode<CustomEventWireModel>(customEventWireModel));
		}

		protected override void Harvest()
		{
			// create new reservoirs to put future events into (we don't want to add events to a reservoir that is being sent)
			var customEvents = _customEvents.Where(node => node != null).Select(node => node.Data).ToList();

			ResetCollections(_customEvents.Size);

			// if we don't have any events to publish then don't
			if (customEvents.Count <= 0)
				return;

			_agentHealthReporter.ReportCustomEventsSent(customEvents.Count);
			var responseStatus = DataTransportService.Send(customEvents);

			HandleResponse(responseStatus, customEvents);
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			ResetCollections(_configuration.CustomEventsMaxSamplesStored);
		}

		private void ResetCollections(UInt32 customEventCollectionCapacity)
		{
			_customEvents = new ConcurrentPriorityQueue<PrioritizedNode<CustomEventWireModel>>(customEventCollectionCapacity);
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, [NotNull] IEnumerable<CustomEventWireModel> customEvents)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.CommunicationError:
				case DataTransportResponseStatus.RequestTimeout:
				case DataTransportResponseStatus.ServerError:
				case DataTransportResponseStatus.ConnectionError:
					RetainEvents(customEvents);
					break;
				case DataTransportResponseStatus.PostTooBigError:
					var newSize = (uint)(customEvents.Count() * ReservoirReductionSizeMultiplier);
					ReduceReservoirSize(newSize);
					RetainEvents(customEvents);
					break;
				case DataTransportResponseStatus.OtherError:
				case DataTransportResponseStatus.RequestSuccessful:
				default:
					break;
			}
		}

		private void RetainEvents([NotNull] IEnumerable<CustomEventWireModel> customEvents)
		{
			customEvents = customEvents.ToList();
			_agentHealthReporter.ReportCustomEventsRecollected(customEvents.Count());

			foreach(var customEvent in customEvents)
			{
				if ( customEvent != null)
				{
					_customEvents.Add(new PrioritizedNode<CustomEventWireModel>(customEvent));
				}
			}
		}

		private void ReduceReservoirSize(UInt32 newSize)
		{
			if (newSize >= _customEvents.Size)
				return;

			_customEvents.Resize(newSize);
			_agentHealthReporter.ReportCustomEventReservoirResized(newSize);
		}
	}
}