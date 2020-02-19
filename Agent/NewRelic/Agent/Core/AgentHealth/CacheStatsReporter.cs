using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Caching;
using NewRelic.Core.Logging;
using System;
using System.Collections.Concurrent;

namespace NewRelic.Agent.Core.AgentHealth
{
	public interface ICacheStatsReporter : IOutOfBandMetricSource
	{
		void RegisterCache(ICacheStats cacheStats, params string[] metricNameParts);
	}

	public class CacheStatsReporter : ICacheStatsReporter
	{
		private readonly IMetricBuilder _metricBuilder;
		private PublishMetricDelegate _publishMetricDelegate;
		private readonly ConcurrentDictionary<ICacheStats, string> _cacheStats = new ConcurrentDictionary<ICacheStats, string>();

		public CacheStatsReporter(IMetricBuilder metricBuilder)
		{
			_metricBuilder = metricBuilder;
		}

		public void CollectMetrics()
		{
			foreach (var stats in _cacheStats.Keys)
			{
				if (stats.CountHits == 0 && stats.CountMisses == 0 && stats.CountEjections == 0 && stats.Size == 0)
				{
					continue;
				}

				var metricNamePrefix = _cacheStats[stats];

				TrySend(_metricBuilder.TryBuildCacheCountMetric(metricNamePrefix + "Hits", stats.CountHits));
				TrySend(_metricBuilder.TryBuildCacheCountMetric(metricNamePrefix + "Misses", stats.CountMisses));
				TrySend(_metricBuilder.TryBuildCacheCountMetric(metricNamePrefix + "Ejections", stats.CountEjections));
				TrySend(_metricBuilder.TryBuildCacheSizeMetric(metricNamePrefix + "Size", stats.Size));
				TrySend(_metricBuilder.TryBuildCacheSizeMetric(metricNamePrefix + "Capacity", stats.Capacity));

				stats.ResetStats();
			}
		}

		public void RegisterCache(ICacheStats cacheStats, params string[] metricNameParts)
		{
			_cacheStats.TryAdd(cacheStats, string.Join("/", metricNameParts) + "/");
		}

		private void TrySend(MetricWireModel metric)
		{
			if (metric == null)
			{
				return;
			}

			if (_publishMetricDelegate == null)
			{
				Log.WarnFormat("No PublishMetricDelegate to flush metric '{0}' through.", metric.MetricName.Name);
				return;
			}

			try
			{
				_publishMetricDelegate(metric);
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
		}

		public void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate)
		{
			if (_publishMetricDelegate != null)
			{
				Log.Warn("Existing PublishMetricDelegate registration being overwritten.");
			}

			_publishMetricDelegate = publishMetricDelegate;
		}
	}
}
