using System.Collections.Generic;
using System.Linq;
using MoreLinq;
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
        void Collect(CustomEventWireModel customEventWireModel);
    }

    /// <summary>
    /// An service for collecting and managing custom events.
    /// </summary>
    public class CustomEventAggregator : AbstractAggregator<CustomEventWireModel>, ICustomEventAggregator
    {
        private const double ReservoirReductionSizeMultiplier = 0.5;
        private readonly IAgentHealthReporter _agentHealthReporter;

        // Note that synethics events must be recorded, and thus are stored in their own unique reservoir to ensure that they are never pushed out by non-synthetics events.
        private IResizableCappedCollection<CustomEventWireModel> _customEvents = new ConcurrentReservoir<CustomEventWireModel>(0);

        public CustomEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter)
            : base(dataTransportService, scheduler, processStatic)
        {
            _agentHealthReporter = agentHealthReporter;
            ResetCollections(_configuration.CustomEventsMaxSamplesStored);
        }

        public override void Collect(CustomEventWireModel customEventWireModel)
        {
            _agentHealthReporter.ReportCustomEventCollected();
            _customEvents.Add(customEventWireModel);
        }

        protected override void Harvest()
        {
            // create new reservoirs to put future events into (we don't want to add events to a reservoir that is being sent)
            var customEvents = _customEvents;

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

        private void ResetCollections(uint customEventCollectionCapacity)
        {
            _customEvents = new ConcurrentReservoir<CustomEventWireModel>(customEventCollectionCapacity);
        }

        private void HandleResponse(DataTransportResponseStatus responseStatus, IEnumerable<CustomEventWireModel> customEvents)
        {
            switch (responseStatus)
            {
                case DataTransportResponseStatus.ServiceUnavailableError:
                case DataTransportResponseStatus.ConnectionError:
                    RetainEvents(customEvents);
                    break;
                case DataTransportResponseStatus.PostTooBigError:
                    ReduceReservoirSize((uint)(customEvents.Count() * ReservoirReductionSizeMultiplier));
                    RetainEvents(customEvents);
                    break;
                case DataTransportResponseStatus.OtherError:
                case DataTransportResponseStatus.RequestSuccessful:
                default:
                    break;
            }
        }

        private void RetainEvents(IEnumerable<CustomEventWireModel> customEvents)
        {
            customEvents = customEvents.ToList();
            _agentHealthReporter.ReportCustomEventsRecollected(customEvents.Count());

            customEvents
                .Where(@event => @event != null)
                .ForEach(_customEvents.Add);
        }

        private void ReduceReservoirSize(uint newSize)
        {
            if (newSize >= _customEvents.Size)
                return;

            _customEvents.Resize(newSize);
            _agentHealthReporter.ReportCustomEventReservoirResized(newSize);
        }
    }
}
