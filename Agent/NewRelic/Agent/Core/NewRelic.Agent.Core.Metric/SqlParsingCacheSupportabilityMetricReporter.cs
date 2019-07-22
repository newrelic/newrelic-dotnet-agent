using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Metric
{
	public interface ISqlParsingCacheSupportabilityMetricReporter : IOutOfBandMetricSource
	{
		void CollectMetrics();
	}

	public class SqlParsingCacheSupportabilityMetricReporter : ISqlParsingCacheSupportabilityMetricReporter
	{
		private static readonly string[] HitSupportabilityNames;
		private static readonly string[] MissSupportabilityNames;
		private static readonly string[] EjectionSupportabilityNames;
		private static readonly string[] SizeSupportabilityNames;
		private const string CapacitySupportabilityMetric = "Capacity";

		private readonly IMetricBuilder _metricBuilder;
		private readonly IDatabaseStatementParser _databaseStatementParser;

		private PublishMetricDelegate _publishMetricDelegate;

		static SqlParsingCacheSupportabilityMetricReporter()
		{
			var values = Enum.GetValues(typeof(DatastoreVendor));
			HitSupportabilityNames = new string[values.Length];
			MissSupportabilityNames = new string[values.Length];
			EjectionSupportabilityNames = new string[values.Length];
			SizeSupportabilityNames = new string[values.Length];

			var hitString = "Hits";
			var missString = "Misses";
			var ejectionString = "Ejections";
			var sizeString = "Size";

			foreach (var value in values)
			{
				HitSupportabilityNames[(int)value] = Enum.GetName(typeof(DatastoreVendor), value) + MetricNames.PathSeparator + hitString;
				MissSupportabilityNames[(int)value] = Enum.GetName(typeof(DatastoreVendor), value) + MetricNames.PathSeparator + missString;
				EjectionSupportabilityNames[(int)value] = Enum.GetName(typeof(DatastoreVendor), value) + MetricNames.PathSeparator + ejectionString;
				SizeSupportabilityNames[(int)value] = Enum.GetName(typeof(DatastoreVendor), value) + MetricNames.PathSeparator + sizeString;
			}
		}

		public SqlParsingCacheSupportabilityMetricReporter(IMetricBuilder metricBuilder, IDatabaseStatementParser databaseStatementParser)
		{
			_metricBuilder = metricBuilder;
			_databaseStatementParser = databaseStatementParser;
		}

		public void CollectMetrics()
		{
			var shouldSendCacheCapacityMetric = false;

			foreach (DatastoreVendor vendor in Enum.GetValues(typeof(DatastoreVendor)))
			{
				var hits = _databaseStatementParser.GetCacheHits(vendor);
				var misses = _databaseStatementParser.GetCacheMisses(vendor);
				var ejections = _databaseStatementParser.GetCacheEjections(vendor);
				var size = _databaseStatementParser.GetCacheSize(vendor);

				if (hits == 0 && misses == 0 && ejections == 0 && size == 0)
				{
					continue;
				}

				shouldSendCacheCapacityMetric = true;

				TrySend(_metricBuilder.TryBuildSqlParsingCacheCountMetric(HitSupportabilityNames[(int)vendor], hits));
				TrySend(_metricBuilder.TryBuildSqlParsingCacheCountMetric(MissSupportabilityNames[(int)vendor], misses));
				TrySend(_metricBuilder.TryBuildSqlParsingCacheCountMetric(EjectionSupportabilityNames[(int)vendor], ejections));
				TrySend(_metricBuilder.TryBuildSqlParsingCacheSizeMetric(SizeSupportabilityNames[(int)vendor], size));
			}

			if (shouldSendCacheCapacityMetric)
			{
				TrySend(_metricBuilder.TryBuildSqlParsingCacheSizeMetric(CapacitySupportabilityMetric, (int)_databaseStatementParser.CacheCapacity));
			}

			_databaseStatementParser.ResetStats();
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
