/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Aggregators
{
    public class MetricStatsCollection
    {
        //stores unscoped stats reported during a transaction 
        private MetricStatsDictionary<string, MetricDataWireModel> _unscopedStats = new MetricStatsDictionary<string, MetricDataWireModel>();
        //store scoped stats reported during a transaction
        // The String key is the scope.
        private Dictionary<string, MetricStatsDictionary<string, MetricDataWireModel>> _scopedStats = new Dictionary<string, MetricStatsDictionary<string, MetricDataWireModel>>();

        //stores unscoped stats reported outside a transaction
        private MetricStatsDictionary<string, MetricWireModel> _preCreatedUnscopedStats = new MetricStatsDictionary<string, MetricWireModel>();

        private Func<MetricDataWireModel, MetricDataWireModel, MetricDataWireModel> _mergeFunction = MetricDataWireModel.BuildAggregateData;
        private Func<MetricWireModel, MetricWireModel, MetricWireModel> _mergeUnscopedFunction = MetricWireModel.Merge;

        public void Merge(MetricStatsCollection engine)
        {
            _unscopedStats.Merge(engine._unscopedStats, _mergeFunction);
            _preCreatedUnscopedStats.Merge(engine._preCreatedUnscopedStats, _mergeUnscopedFunction);
            foreach (KeyValuePair<string, MetricStatsDictionary<string, MetricDataWireModel>> current in engine._scopedStats)
            {
                this.MergeScopedStats(current.Key, current.Value);
            }


        }

        public void MergeUnscopedStats(IEnumerable<KeyValuePair<string, MetricDataWireModel>> unscoped)
        {
            _unscopedStats.Merge(unscoped, _mergeFunction);
        }

        // These should have already gone through the prenaming process
        public void MergeUnscopedStats(MetricWireModel metric)
        {
            _preCreatedUnscopedStats.Merge(metric.MetricName.Name, metric, _mergeUnscopedFunction);
        }

        public void MergeUnscopedStats(string name, MetricDataWireModel metric)
        {
            _unscopedStats.Merge(name, metric, _mergeFunction);
        }

        public void MergeScopedStats(string scope, string name, MetricDataWireModel metric)
        {
            MetricStatsDictionary<string, MetricDataWireModel> alreadyScoped;
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

        public void MergeScopedStats(string scope, IEnumerable<KeyValuePair<string, MetricDataWireModel>> metrics)
        {
            MetricStatsDictionary<string, MetricDataWireModel> alreadyScoped;
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

            foreach (KeyValuePair<string, MetricStatsDictionary<string, MetricDataWireModel>> currentScope in _scopedStats)
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
