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

    /// <summary>
    /// Applies the metric rename rules to every metric in this collection and returns the results, ready
    /// to be serialized and sent.
    ///
    /// Everything up to this point aggregates on pre-rename metric names, so two distinct names can collapse
    /// onto a single name once the rules are applied. Renamed metrics are therefore re-keyed and re-merged
    /// here; without that, colliding metrics would be sent as separate metrics that happen to share a name,
    /// each carrying only its own portion of the data, instead of as one aggregate.
    /// </summary>
    public IList<MetricWireModel> ConvertToJsonForSending(IMetricNameService nameService)
    {
        var metrics = new List<MetricWireModel>();

        foreach (KeyValuePair<string, MetricDataWireModel> current in RenameAndMerge(_unscopedStats, nameService))
        {
            metrics.Add(MetricWireModel.BuildMetric(current.Key, null, current.Value));
        }

        foreach (KeyValuePair<string, MetricStatsDictionary<string, MetricDataWireModel>> currentScope in _scopedStats)
        {
            foreach (KeyValuePair<string, MetricDataWireModel> currentMetric in RenameAndMerge(currentScope.Value, nameService))
            {
                metrics.Add(MetricWireModel.BuildMetric(currentMetric.Key, currentScope.Key, currentMetric.Value));
            }
        }

        return metrics;
    }

    /// <summary>
    /// Returns a copy of <paramref name="stats"/> keyed on post-rename metric names, merging the data of any
    /// metrics whose names collide once renamed. Metrics that the rename rules mark as ignored are dropped.
    /// </summary>
    private MetricStatsDictionary<string, MetricDataWireModel> RenameAndMerge(IEnumerable<KeyValuePair<string, MetricDataWireModel>> stats, IMetricNameService nameService)
    {
        var renamedStats = new MetricStatsDictionary<string, MetricDataWireModel>();

        foreach (KeyValuePair<string, MetricDataWireModel> current in stats)
        {
            // MetricNameService returns null if the metric needs to be ignored
            var newName = nameService.RenameMetric(current.Key);
            if (newName == null)
            {
                continue;
            }

            renamedStats.Merge(newName, current.Value, _mergeFunction);
        }

        return renamedStats;
    }
}
