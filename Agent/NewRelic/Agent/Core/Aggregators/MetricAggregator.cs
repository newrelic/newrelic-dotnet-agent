using System;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.SystemInterfaces;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Aggregators
{
	public interface IMetricAggregator
	{
		void Collect(IAllMetricStatsCollection metric);
	}

	public class MetricAggregator : AbstractAggregator<IAllMetricStatsCollection>, IMetricAggregator
	{
		private MetricStatsEngineQueue _metricStatsEngineQueue;
		private readonly IMetricBuilder _metricBuilder;
		private readonly IMetricNameService _metricNameService;
		private readonly IEnumerable<IOutOfBandMetricSource> _outOfBandMetricSources;

		public MetricAggregator(IDataTransportService dataTransportService, IMetricBuilder metricBuilder, IMetricNameService metricNameService, IEnumerable<IOutOfBandMetricSource> outOfBandMetricSources, IProcessStatic processStatic, IScheduler scheduler)
			: base(dataTransportService, scheduler, processStatic)
		{
			_metricBuilder = metricBuilder;
			_metricNameService = metricNameService;
			_outOfBandMetricSources = outOfBandMetricSources;

			foreach (var source in outOfBandMetricSources)
			{
				if (source != null)
				{
					source.RegisterPublishMetricHandler(Collect);
				}
			}

			_metricStatsEngineQueue = CreateMetricStatsEngineQueue();
		}

		public MetricStatsEngineQueue StatsEngineQueue => _metricStatsEngineQueue;

		protected override bool IsEnabled => true;

		#region interface and abstract override required methods

		public override void Collect(IAllMetricStatsCollection metric)
		{
			bool done = false;
			while (!done)
			{
				done = _metricStatsEngineQueue.MergeMetric(metric);
			}
		}
		protected override void Harvest()
		{
			Log.Info("Metric harvest starting.");

			foreach (var source in _outOfBandMetricSources)
			{
				source.CollectMetrics();
			}

			var oldMetrics = GetStatsEngineForHarvest();

			oldMetrics.MergeUnscopedStats(_metricBuilder.TryBuildMetricHarvestAttemptMetric());
			var metricsToSend = oldMetrics.ConvertToJsonForSending(_metricNameService);

			var responseStatus = DataTransportService.Send(metricsToSend);
			HandleResponse(responseStatus, metricsToSend);

			Log.Debug("Metric harvest finished.");
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			// It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
			// If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

			ReplaceStatsEngineQueue();
		}

		#endregion

		private void HandleResponse(DataTransportResponseStatus responseStatus, IEnumerable<MetricWireModel> unsuccessfulSendMetrics)
		{
			switch (responseStatus)
			{
				case DataTransportResponseStatus.RequestSuccessful:
					break;
				case DataTransportResponseStatus.Retain:
					RetainMetricData(unsuccessfulSendMetrics);
					break;
				case DataTransportResponseStatus.ReduceSizeIfPossibleOtherwiseDiscard:
				case DataTransportResponseStatus.Discard:
				default:
					break;
			}
		}

		private void RetainMetricData(IEnumerable<MetricWireModel> unsuccessfulSendMetrics)
		{
			foreach (var metric in unsuccessfulSendMetrics)
			{
				Collect(metric);
			}
		}

		/// <summary>
		/// Replaces the current MetricStatsEngineQueue with a new one and combines all the StatsEngines in the 
		/// old queue into a single MetricStatsCollection that can serve as the source of aggregated metrics to
		/// send to the collector.
		/// </summary>
		/// <returns></returns>
		private MetricStatsCollection GetStatsEngineForHarvest()
		{
			MetricStatsEngineQueue oldMetricStatsEngineQueue = ReplaceStatsEngineQueue();
			return oldMetricStatsEngineQueue.GetStatsEngineForHarvest();
		}

		private MetricStatsEngineQueue ReplaceStatsEngineQueue()
		{
			MetricStatsEngineQueue oldMetricStatsEngineQueue = _metricStatsEngineQueue;
			_metricStatsEngineQueue = CreateMetricStatsEngineQueue();
			return oldMetricStatsEngineQueue;
		}

		private MetricStatsEngineQueue CreateMetricStatsEngineQueue()
		{
			return new MetricStatsEngineQueue();
		}

		/// <summary>
		/// Ported, with some tweaks, from the Java Agent.  Allows the agent to use multiple StatsEngines (Dictionaries) to aggregate
		/// metrics between harvests, so that a lock on a single Dictionary does not become a throughput bottleneck at high load.
		/// At harvest time all the Dictionaries are combined.
		/// </summary>
		public class MetricStatsEngineQueue
		{
			private int _statsEngineCount;

			private Queue<MetricStatsCollection> _statsEngineQueue;
			// at harvest time readers drain and a write lock allows the entire queue to be swapped out
			private readonly ReaderWriterLockSlim _lock;
			// this lock is to ensure that only one reader at a time can dequeue/enqueue stats engines from/to the queue,
			// since Queue is not threadsafe an ConcurrentQueue was not available until .NET 4.0
			private readonly object _queueReadersLock;

			internal MetricStatsEngineQueue()
			{
				_statsEngineQueue = new Queue<MetricStatsCollection>();
				_lock = new ReaderWriterLockSlim();
				_queueReadersLock = new object();
			}

			public int StatsEngineCount => _statsEngineCount;

			/// <summary>
			/// This MetricStatsEngineQueue uses a readwrite lock (allows multiple readers, but only one writer, at a time) to mediate
			/// between metrics that need to merge into one of the engines (readers), and the harvest job (writer), which replaces the 
			/// queue and merges the engines in the old queue to create the harvest payload.
			/// 
			/// In this method, get the read lock and call a method that will pick an engine and merge metric.
			/// </summary>
			/// <param name="metric"></param>
			/// <returns></returns>
			public bool MergeMetric(IAllMetricStatsCollection metric)
			{
				if (_lock.TryEnterReadLock(50))
				{
					try
					{
						Queue<MetricStatsCollection> statsEngineQueue = this._statsEngineQueue;
						if (statsEngineQueue == null)
						{
							// We've already been harvested.  Caller should try again, at which point a whole new MetricStatsEngineQueue
							// will have been created for the next harvest cycle.
							return false;
						}
						MergeMetricUnderLock(statsEngineQueue, metric);
						return true;
					}
					finally
					{
						_lock.ExitReadLock();
					}
				}
				return false;
			}

			/// <summary>
			/// Take a stats engine (metrics aggregate Dictionary) off the queue and merge metric into it, then put it back
			/// on the queue.  Create one if all the existing ones are "checked out" -- others can use it later.
			///  
			/// We are one of (possibly) several readers of the stats engine queue under the readwrite lock, so harvest will
			/// wait for us to finish before fetching/replacing the entire queue
			/// </summary>
			/// <param name="statsEngineQueue"></param>
			/// <param name="metric"></param>
			private void MergeMetricUnderLock(Queue<MetricStatsCollection> statsEngineQueue, IAllMetricStatsCollection metrics)
			{
				MetricStatsCollection statsEngineToMergeWith = null;
				try
				{
					lock (_queueReadersLock)
					{
						if (statsEngineQueue.Count > 0)
						{
							statsEngineToMergeWith = statsEngineQueue.Dequeue();
						}
					}

					// make a new stats engine if there aren't enough to go around right now
					if (statsEngineToMergeWith == null)
					{
						statsEngineToMergeWith = CreateMetricStatsEngine();
						Interlocked.Increment(ref _statsEngineCount);
					}

					metrics.AddMetricsToEngine(statsEngineToMergeWith);
				}
				catch (Exception e)
				{
					Log.Warn($"Exception dequeueing/creating stats engine: {e}");
				}
				finally
				{
					if (statsEngineToMergeWith != null)
					{
						try
						{
							lock (_queueReadersLock)
							{
								statsEngineQueue.Enqueue(statsEngineToMergeWith);
							}
						}
						catch (Exception e)
						{
							// should never happen
							Log.Warn($"Exception returning stats engine to queue: {e}");
						}
					}
				}
			}

			/// <summary>
			/// Null out the Queue contained in this MetricStatsEngineQueue under writelock so that any subsequent
			/// attempts to read from the queue before this MetricStatsEngineQueue is replaced will fail.  Combine
			/// all the StatsEngines in the old Queue according to the merge function, and return the result. 
			/// </summary>
			/// <returns></returns>
			public MetricStatsCollection GetStatsEngineForHarvest()
			{
				Queue<MetricStatsCollection> statsEngineQueue;
				_lock.EnterWriteLock();
				try
				{
					statsEngineQueue = this._statsEngineQueue;

					//
					// Clear the reference to the queue so that future calls to doStatsWork() get short-circuited.
					//
					this._statsEngineQueue = null;
				}
				finally
				{
					_lock.ExitWriteLock();
				}

				//
				// Operations on statsEngineQueue will only occur within an acquired readLock and we've short-circuited
				// any threads that might be racing for it, so safe to do the bulk of the harvest work outside of writeLock.
				//
				return GetStatsEngineForHarvest(statsEngineQueue);
			}

			private MetricStatsCollection GetStatsEngineForHarvest(Queue<MetricStatsCollection> statsEngines)
			{

				MetricStatsCollection harvestMetricsStatsEngine = CreateMetricStatsEngine();

				int actualStatsEngineCount = 0;
				foreach (MetricStatsCollection statsEngine in statsEngines)
				{
					harvestMetricsStatsEngine.Merge(statsEngine);
					actualStatsEngineCount++;
				}

				if (actualStatsEngineCount != _statsEngineCount)
				{
					Log.Warn($"Error draining stats engine queue. Expected: {_statsEngineCount} actual: {actualStatsEngineCount}");
				}

				return harvestMetricsStatsEngine;
			}

			private MetricStatsCollection CreateMetricStatsEngine()
			{
				return new MetricStatsCollection();
			}
		}
	}
}
