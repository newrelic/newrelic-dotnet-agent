using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Collections;
using NewRelic.SystemInterfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface ISpanEventAggregator
	{
		void Collect(SpanEventWireModel wireModel);
		void Collect(IEnumerable<SpanEventWireModel> wireModels);
	}

	public class SpanEventAggregator : AbstractAggregator<SpanEventWireModel>, ISpanEventAggregator
	{
		private const double ReservoirReductionSizeMultiplier = 0.5;
		private readonly IAgentHealthReporter _agentHealthReporter;

		private ConcurrentPriorityQueue<PrioritizedNode<SpanEventWireModel>> _spanEvents = new ConcurrentPriorityQueue<PrioritizedNode<SpanEventWireModel>>(0);

		/// <summary>
		/// Atomically set a new ConcurrentPriorityQueue to _spanEvents and return the previous ConcurrentPriorityQueue reference;
		/// </summary>
		/// <returns>A reference to the previous ConcurrentPriorityQueue</returns>
		private ConcurrentPriorityQueue<PrioritizedNode<SpanEventWireModel>> GetAndResetCollection()
		{
			return Interlocked.Exchange(ref _spanEvents, new ConcurrentPriorityQueue<PrioritizedNode<SpanEventWireModel>>(_configuration.SpanEventsMaxSamplesStored));
		}

		private int AddWireModels(IEnumerable<SpanEventWireModel> wireModels)
		{
			var nodes = wireModels.Where(model => null != model)
				.Select(model => new PrioritizedNode<SpanEventWireModel>(model));
			return _spanEvents.Add(nodes);
		}

		public SpanEventAggregator(IDataTransportService dataTransportService, IScheduler scheduler, IProcessStatic processStatic, IAgentHealthReporter agentHealthReporter) 
			: base(dataTransportService, scheduler, processStatic)
		{
			_agentHealthReporter = agentHealthReporter;
			//we don't care about the returned CPQ because it was initialized with zero size.
			GetAndResetCollection();
		}

		public override void Collect(SpanEventWireModel wireModel)
		{
			_agentHealthReporter.ReportSpanEventCollected(1);
			_spanEvents.Add(new PrioritizedNode<SpanEventWireModel>(wireModel));
		}

		public void Collect(IEnumerable<SpanEventWireModel> wireModels)
		{
			var added = AddWireModels(wireModels);
			_agentHealthReporter.ReportSpanEventCollected(added);
		}

		protected override void Harvest()
		{
			// Retrieve the number of add attempts before resetting the collection.
			var eventHarvestData = new EventHarvestData(_spanEvents.Size, (uint)_spanEvents.GetAddAttemptsCount());

			//get the list of span events and reset the CPQ atomically
			var spanEventsPriorityQueue = GetAndResetCollection();
			var wireModels = spanEventsPriorityQueue.Where(node => null != node).Select(node => node.Data).ToList();

			// if we don't have any events to publish then don't
			if (wireModels.Count <= 0)
				return;

			_agentHealthReporter.ReportSpanEventsSent(wireModels.Count);
			var responseStatus = DataTransportService.Send(eventHarvestData, wireModels);

			HandleResponse(responseStatus, wireModels);
		}

		private void ReduceReservoirSize(uint newSize)
		{
			if (newSize >= _spanEvents.Size)
				return;

			_spanEvents.Resize(newSize);
		}

		private void RetainEvents(IEnumerable<SpanEventWireModel> wireModels)
		{
			AddWireModels(wireModels);
		}

		private void HandleResponse(DataTransportResponseStatus responseStatus, ICollection<SpanEventWireModel> spanEvents)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.CommunicationError:
				case DataTransportResponseStatus.RequestTimeout:
				case DataTransportResponseStatus.ServerError:
				case DataTransportResponseStatus.ConnectionError:
					RetainEvents(spanEvents);
					break;
				case DataTransportResponseStatus.PostTooBigError:
					ReduceReservoirSize((uint)(spanEvents.Count * ReservoirReductionSizeMultiplier));
					RetainEvents(spanEvents);
					break;
				case DataTransportResponseStatus.RequestSuccessful:
				case DataTransportResponseStatus.OtherError:
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