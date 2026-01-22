// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Aggregators;

// NOTE: There is no thread safety built in to this class. If using it across multiple threads (as in SqlTraceAggregator),
// lock before calling Insert() or Merge().
public class SqlTraceStatsCollection
{
    private IDictionary<long, SqlTraceWireModel> _sqlTraceWireModels = new Dictionary<long, SqlTraceWireModel>();

    private int _maxTraces;
    private int _tracesCollected = 0;
    private SqlTraceWireModel _shortestMax;

    public SqlTraceStatsCollection(int maxTraces = 100)
    {
        _maxTraces = maxTraces;
    }

    public IDictionary<long, SqlTraceWireModel> Collection => _sqlTraceWireModels;

    public int TracesCollected => _tracesCollected;

    public void Merge(SqlTraceStatsCollection newTraces)
    {
        foreach (var item in newTraces.Collection)
        {
            Insert(item.Value);
        }
    }

    public void Insert(SqlTraceWireModel newSqlTrace)
    {
        _tracesCollected++;

        // If there is no SQL trace already stored with this ID then just store it
        var existingSqlTrace = _sqlTraceWireModels.GetValueOrDefault(newSqlTrace.SqlId);
        if (existingSqlTrace == null)
        {
            TryAdd(newSqlTrace);
            return;
        }

        // If there is already a SQL trace stored with the given SqlId then we need to aggregate the new trace into the existing one
        SqlTraceWireModel dominantSqlTrace;
        if (newSqlTrace.MaxCallTime > existingSqlTrace.MaxCallTime)
        {
            if (_shortestMax != null && _shortestMax.SqlId == newSqlTrace.SqlId)
            {
                _shortestMax = null; // invalidate the cache
            }
            dominantSqlTrace = newSqlTrace;
        }
        else
        {
            dominantSqlTrace = existingSqlTrace;
        }
        var transactionName = dominantSqlTrace.TransactionName;
        var uri = dominantSqlTrace.Uri;
        var sqlId = dominantSqlTrace.SqlId;
        var sql = dominantSqlTrace.Sql;
        var metricName = dominantSqlTrace.DatastoreMetricName;
        var callCount = newSqlTrace.CallCount + existingSqlTrace.CallCount;
        var totalCallTime = newSqlTrace.TotalCallTime + existingSqlTrace.TotalCallTime;
        var minCallTime = (newSqlTrace.MinCallTime < existingSqlTrace.MinCallTime) ? newSqlTrace.MinCallTime : existingSqlTrace.MinCallTime;
        var maxCallTime = dominantSqlTrace.MaxCallTime;
        var parameterData = dominantSqlTrace.ParameterData;

        var mergedSqlTrace = new SqlTraceWireModel(transactionName, uri, sqlId, sql, metricName, callCount, totalCallTime, minCallTime, maxCallTime, parameterData);
        _sqlTraceWireModels[mergedSqlTrace.SqlId] = mergedSqlTrace;
    }

    // Adds the new trace: 1. if fewer than max; or 2. if the list max reached but the new trace is slowest one on the list;
    private void TryAdd(SqlTraceWireModel newSqlTrace)
    {
        // Add new one if the list is not full
        if (_sqlTraceWireModels.Count < _maxTraces)
        {
            _sqlTraceWireModels[newSqlTrace.SqlId] = newSqlTrace;
            return;
        }

        if (_shortestMax == null)
        {
            SqlTraceWireModel shortest = null;
            SqlTraceWireModel secondShortest = null;

            // get the 2 shortest values: 1 to  remove, and 1 to replace cached _shortestMax
            foreach (SqlTraceWireModel trace in _sqlTraceWireModels.Values)
            {
                if (shortest == null)
                {
                    shortest = trace;
                }
                else if (trace.MaxCallTime < shortest.MaxCallTime)
                {
                    secondShortest = shortest;
                    shortest = trace;
                }
                else if (secondShortest == null)
                {
                    secondShortest = trace;
                }
                else if (trace.MaxCallTime < secondShortest.MaxCallTime)
                {
                    secondShortest = trace;
                }
            }

            _sqlTraceWireModels.Remove(shortest.SqlId);
            _shortestMax = secondShortest;
            _sqlTraceWireModels[newSqlTrace.SqlId] = newSqlTrace;
        }
        else if (newSqlTrace.MaxCallTime > _shortestMax.MaxCallTime)
        {
            _sqlTraceWireModels.Remove(_shortestMax.SqlId);
            _sqlTraceWireModels[newSqlTrace.SqlId] = newSqlTrace;

            _shortestMax = null;

            foreach (var trace in _sqlTraceWireModels.Values)
            {
                if (_shortestMax == null)
                {
                    _shortestMax = trace;
                }
                else if (_shortestMax.MaxCallTime > trace.MaxCallTime)
                {
                    _shortestMax = trace;
                }
            }

        }
    }
}
