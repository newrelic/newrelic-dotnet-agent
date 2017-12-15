using JetBrains.Annotations;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Agent.Core.Aggregators
{
	/// <summary>
	/// Reports all metrics recorded during a transaction.
	/// </summary>
	public class TransactionMetricStatsCollection : IAllMetricStatsCollection
	{
		private MetricStatsDictionary<MetricName, MetricDataWireModel> unscopedStats = new MetricStatsDictionary<MetricName, MetricDataWireModel>();
		private MetricStatsDictionary<MetricName, MetricDataWireModel> scopedStats = new MetricStatsDictionary<MetricName, MetricDataWireModel>();
		private readonly TransactionMetricName transactionName;
		private Func<MetricDataWireModel, MetricDataWireModel, MetricDataWireModel> mergeFunction = MetricDataWireModel.BuildAggregateData;


		public TransactionMetricStatsCollection(TransactionMetricName txName)
		{
			transactionName = txName;
		}

		public TransactionMetricName GetTransactionName()
		{
			return transactionName;
		}

		[ItemCanBeNull]
		public MetricDataWireModel GetUnscopedStat(MetricName name)
		{
			unscopedStats.TryGetValue(name, out MetricDataWireModel output);
			return output;
		}

		public void MergeUnscopedStats(MetricName name, [NotNull] MetricDataWireModel metric)
		{
			if (name != null)
			{
				unscopedStats.Merge(name, metric, mergeFunction);
			}
		}

		public void MergeScopedStats(MetricName name, [NotNull] MetricDataWireModel metric)
		{
			if (name != null)
			{
				scopedStats.Merge(name, metric, mergeFunction);
			}
		}

		public void AddMetricsToEngine(MetricStatsCollection engine)
		{
			engine.MergeUnscopedStats(ConvertMetricNames(unscopedStats));
			engine.MergeScopedStats(transactionName.PrefixedName, ConvertMetricNames(scopedStats));
		}

		private IEnumerable<KeyValuePair<string, MetricDataWireModel>> ConvertMetricNames(IEnumerable<KeyValuePair<MetricName, MetricDataWireModel>> metricData)
		{
			foreach (var kvp in metricData)
			{
				yield return new KeyValuePair<string, MetricDataWireModel>(kvp.Key.ToString(), kvp.Value);
			}
		}

		public MetricStatsDictionary<String, MetricDataWireModel> GetUnscopedForTesting()
		{
			var toReturn = new MetricStatsDictionary<String, MetricDataWireModel>();
			foreach (var current in unscopedStats)
			{
				toReturn[current.Key.ToString()] = current.Value;
			}
			return toReturn;
		}

		public MetricStatsDictionary<String, MetricDataWireModel> GetScopedForTesting()
		{
			var toReturn = new MetricStatsDictionary<String, MetricDataWireModel>();
			foreach (var current in scopedStats)
			{
				toReturn[current.Key.ToString()] = current.Value;
			}
			return toReturn;
		}
	}
}
