// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers;

public interface IThreadStatsSampleTransformer
{
    void Transform(ThreadpoolUsageStatsSample threadpoolStats);
    void Transform(ThreadpoolThroughputEventsSample threadpoolEventStats);
}


public class ThreadStatsSampleTransformer : IThreadStatsSampleTransformer
{
    private readonly IMetricBuilder _metricBuilder;

    private readonly IMetricAggregator _metricAggregator;

    public ThreadStatsSampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator)
    {
        _metricBuilder = metricBuilder;
        _metricAggregator = metricAggregator;
    }

    public void Transform(ThreadpoolUsageStatsSample threadpoolStats)
    {
        var workerThreadsAvail = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Worker, ThreadStatus.Available, threadpoolStats.WorkerCountThreadsAvail);
        var workerThreadsUsed = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Worker, ThreadStatus.InUse, threadpoolStats.WorkerCountThreadsUsed);
        var completionThreadsAvail = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Completion, ThreadStatus.Available, threadpoolStats.CompletionCountThreadsAvail);
        var completionThreadsUsed = _metricBuilder.TryBuildThreadpoolUsageStatsMetric(ThreadType.Completion, ThreadStatus.InUse, threadpoolStats.CompletionCountThreadsUsed);

        RecordMetrics(workerThreadsAvail, workerThreadsUsed, completionThreadsAvail, completionThreadsUsed);
    }

    public void Transform(ThreadpoolThroughputEventsSample throughputStats)
    {
        var metricRequested = _metricBuilder.TryBuildThreadpoolThroughputStatsMetric(ThreadpoolThroughputStatsType.Requested, throughputStats.CountThreadRequestsQueued);
        var metricStarted = _metricBuilder.TryBuildThreadpoolThroughputStatsMetric(ThreadpoolThroughputStatsType.Started, throughputStats.CountThreadRequestsDequeued);
        var metricQueueLength = _metricBuilder.TryBuildThreadpoolThroughputStatsMetric(ThreadpoolThroughputStatsType.QueueLength, throughputStats.ThreadRequestQueueLength);

        RecordMetrics(metricRequested, metricStarted, metricQueueLength);
    }

    private void RecordMetrics(params MetricWireModel[] metrics)
    {
        foreach (var metric in metrics)
        {
            _metricAggregator.Collect(metric);
        }
    }
}