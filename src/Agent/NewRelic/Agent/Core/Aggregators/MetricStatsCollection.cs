using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Aggregators
{
    public class MetricStatsCollection
    {
        //stores unscoped stats reported during a transaction 
        private MetricStatsDictionary<String, MetricDataWireModel> _unscopedStats = new MetricStatsDictionary<String, MetricDataWireModel>();
        //store scoped stats reported during a transaction
        // The String key is the scope.
        private Dictionary<String, MetricStatsDictionary<String, MetricDataWireModel>> _scopedStats = new Dictionary<String, MetricStatsDictionary<String, MetricDataWireModel>>();

        //stores unscoped stats reported outside a transaction
        private MetricStatsDictionary<String, MetricWireModel> _preCreatedUnscopedStats = new MetricStatsDictionary<String, MetricWireModel>();

        private Func<MetricDataWireModel, MetricDataWireModel, MetricDataWireModel> _mergeFunction = MetricDataWireModel.BuildAggregateData;
        private Func<MetricWireModel, MetricWireModel, MetricWireModel> _mergeUnscopedFunction = MetricWireModel.Merge;

        public void Merge([NotNull] MetricStatsCollection engine)
        {
            _unscopedStats.Merge(engine._unscopedStats, _mergeFunction);
            _preCreatedUnscopedStats.Merge(engine._preCreatedUnscopedStats, _mergeUnscopedFunction);
            foreach (KeyValuePair<string, MetricStatsDictionary<String, MetricDataWireModel>> current in engine._scopedStats)
            {
                this.MergeScopedStats(current.Key, current.Value);
            }


        }

        public void MergeUnscopedStats([NotNull] IEnumerable<KeyValuePair<string, MetricDataWireModel>> unscoped)
        {
            _unscopedStats.Merge(unscoped, _mergeFunction);
        }

        // These should have already gone through the prenaming process
        public void MergeUnscopedStats([NotNull] MetricWireModel metric)
        {
            _preCreatedUnscopedStats.Merge(metric.MetricName.Name, metric, _mergeUnscopedFunction);
        }

        public void MergeUnscopedStats(String name, [NotNull] MetricDataWireModel metric)
        {
            _unscopedStats.Merge(name, metric, _mergeFunction);
        }

        public void MergeScopedStats(String scope, String name, MetricDataWireModel metric)
        {
            MetricStatsDictionary<String, MetricDataWireModel> alreadyScoped;
            if (_scopedStats.TryGetValue(scope, out alreadyScoped))
            {
                alreadyScoped.Merge(name, metric, _mergeFunction);
            }
            else
            {
                alreadyScoped = new MetricStatsDictionary<string, MetricDataWireModel>();
                alreadyScoped.Merge(name, metric, _mergeFunction);
                _scopedStats[scope] = alreadyScoped;
            }
        }

        public void MergeScopedStats(String scope, IEnumerable<KeyValuePair<string, MetricDataWireModel>> metrics)
        {
            MetricStatsDictionary<String, MetricDataWireModel> alreadyScoped;
            if (_scopedStats.TryGetValue(scope, out alreadyScoped))
            {
                alreadyScoped.Merge(metrics, _mergeFunction);
            }
            else
            {
                alreadyScoped = new MetricStatsDictionary<string, MetricDataWireModel>(metrics);
                _scopedStats[scope] = alreadyScoped;
            }
        }

        public IEnumerable<MetricWireModel> ConvertToJsonForSending(IMetricNameService nameService)
        {
            foreach (KeyValuePair<string, MetricDataWireModel> current in _unscopedStats)
            {
                var metric = MetricWireModel.BuildMetric(nameService, current.Key, null, current.Value);
                if (metric != null)
                {
                    yield return metric;
                }
            }

            foreach (MetricWireModel model in _preCreatedUnscopedStats.Values)
            {
                //These already when through the rename service in the MetricWireModel
                //At some point this needs to be cleaned up. This is ugly.

                yield return model;
            }

            foreach (KeyValuePair<string, MetricStatsDictionary<String, MetricDataWireModel>> currentScope in _scopedStats)
            {
                foreach (KeyValuePair<string, MetricDataWireModel> currentMetric in currentScope.Value)
                {
                    var metric = MetricWireModel.BuildMetric(nameService, currentMetric.Key, currentScope.Key, currentMetric.Value);
                    if (metric != null)
                    {
                        yield return metric;
                    }
                }
            }
        }
    }
}
