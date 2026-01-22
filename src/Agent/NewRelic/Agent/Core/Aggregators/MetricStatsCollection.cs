// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Aggregators;

public class MetricStatsCollection
{
    //stores unscoped stats reported during a transaction 
    private MetricStatsDictionary<string, MetricDataWireModel> _unscopedStats = new MetricStatsDictionary<string, MetricDataWireModel>();

    //store scoped stats reported during a transaction
    // The String key is the scope.
    private Dictionary<string, MetricStatsDictionary<string, MetricDataWireModel>> _scopedStats = new Dictionary<string, MetricStatsDictionary<string, MetricDataWireModel>>();

    private Func<MetricDataWireModel, MetricDataWireModel, MetricDataWireModel> _mergeFunction = MetricDataWireModel.BuildAggregateData;

    public void Merge(MetricStatsCollection engine)
    {
        _unscopedStats.Merge(engine._unscopedStats, _mergeFunction);
        foreach (KeyValuePair<string, MetricStatsDictionary<string, MetricDataWireModel>> current in engine._scopedStats)
        {
            MergeScopedStats(current.Key, current.Value);
        }
    }

    public void MergeUnscopedStats(IEnumerable<KeyValuePair<string, MetricDataWireModel>> unscoped)
    {
        _unscopedStats.Merge(unscoped, _mergeFunction);
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
