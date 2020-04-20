using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface ISpanEventAggregatorInfiniteTracing
	{
		void Collect(Span wireModel);
		void Collect(IEnumerable<Span> wireModels);
		bool IsServiceEnabled { get; }
		bool IsServiceAvailable { get; }
	}

	public class SpanEventAggregatorInfiniteTracing : DisposableService, ISpanEventAggregatorInfiniteTracing
	{
		private BlockingCollection<Span> _spanEvents;
		private readonly IDataStreamingService<Span, RecordStatus> _spanStreamingService;
		private readonly IAgentHealthReporter _agentHealthReporter;
		private readonly IConfigurationService _configSvc;
		private IConfiguration _configuration => _configSvc?.Configuration;

		public SpanEventAggregatorInfiniteTracing(IDataStreamingService<Span, RecordStatus> spanStreamingService, IConfigurationService configSvc, IAgentHealthReporter agentHealthReporter)
		{
			_spanStreamingService = spanStreamingService;
			_subscriptions.Add<AgentConnectedEvent>(AgentConnected);
			_agentHealthReporter = agentHealthReporter;
			_configSvc = configSvc;
		}

		/// <summary>
		/// This executes every time a local configuration change is made.  It is more convenient
		/// that OnConfigurationChanged
		/// </summary>
		/// <param name="_"></param>
		private void AgentConnected(AgentConnectedEvent _)
		{
			_spanStreamingService.Shutdown();
			var newCapacity = _configuration.InfiniteTracingQueueSizeSpans;
			
			if (!IsServiceEnabled || newCapacity <= 0)
			{
				if (_spanEvents != null)
				{
					var oldqueue = Interlocked.Exchange(ref _spanEvents, null);
					RecordDroppedSpans(oldqueue.Count);
				}
				return;
			}

			if (_spanEvents == null || newCapacity != _spanEvents.BoundedCapacity)
			{
				var oldCollection = _spanEvents;

				if (_spanEvents != null)
				{
					Interlocked.Exchange(ref _spanEvents, new BlockingCollection<Span>(new ConcurrentQueue<Span>(_spanEvents.ToArray().Take(newCapacity)), newCapacity));
				}
				else
				{
					_spanEvents = new BlockingCollection<Span>(newCapacity);
				}

				if (oldCollection != null && oldCollection.Count > newCapacity)
				{
					var countDropped = oldCollection.Count - newCapacity;
					RecordDroppedSpans(countDropped);
				}
			}


			LogConfiguration();

			_spanStreamingService.StartConsumingCollection(_spanEvents);
		}

		private void LogConfiguration()
		{
			Log.Info($"SpanEventAggregatorInfiniteTracing: Configuration Setting - Queue Size - {_configuration.InfiniteTracingQueueSizeSpans}");
		}
	
		public bool IsServiceEnabled => _spanStreamingService.IsServiceEnabled;
		public bool IsServiceAvailable => IsServiceEnabled && _spanEvents != null && _spanStreamingService.IsServiceAvailable;

		private void RecordDroppedSpans(int countDroppedSpans)
		{
			_agentHealthReporter.ReportInfiniteTracingSpanEventsDropped(countDroppedSpans);
		}

		public void Collect(Span wireModel)
		{
			_agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(1);

			if (_spanEvents == null || !_spanEvents.TryAdd(wireModel))
			{
				RecordDroppedSpans(1);
			}
		}

		public void Collect(IEnumerable<Span> wireModels)
		{
			if (_spanEvents == null)
			{
				var countSpans = wireModels.Count();

				_agentHealthReporter.ReportInfiniteTracingSpanEventsSeen(countSpans);
				RecordDroppedSpans(countSpans);
				
				return;
			}

			foreach (var span in wireModels)
			{
				Collect(span);
			}
		}
	}
}
