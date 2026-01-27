// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers;

public interface IMemorySampleTransformer
{
    void Transform(ImmutableMemorySample sample);
}

public class MemorySampleTransformer : IMemorySampleTransformer
{
    private readonly IMetricBuilder _metricBuilder;

    private readonly IMetricAggregator _metricAggregator;

    private readonly bool _isWindows;


    public MemorySampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator, AgentInstallConfiguration.IsWindowsDelegate getIsWindows)
    {
        _metricBuilder = metricBuilder;
        _metricAggregator = metricAggregator;
        _isWindows = getIsWindows();
    }

    public void Transform(ImmutableMemorySample sample)
    {
        //Physical memory on windows is measured by PrivateBytes but on Linux it is better measured using WorkingSet.
        //This will allow us to report memory usage that more closely resembles the memory usage provided by tools
        //on both operating systems.
        //The physical memory metric is what supported out of the box by the APM UI.

        // Do not create metrics if memory values are 0
        // Value may be 0 due to lack of support on called platform (i.e. Linux does not provide Process.PrivateMemorySize64 for older versions of .net core)
        if (sample.MemoryPrivate > 0 && _isWindows)
        {
            RecordMemoryPhysicalMetric(sample.MemoryPrivate);
        }

        if (sample.MemoryWorkingSet > 0)
        {
            RecordMemoryWorkingSetMetric(sample.MemoryWorkingSet);

            if (!_isWindows)
            {
                RecordMemoryPhysicalMetric(sample.MemoryWorkingSet);
            }
        }
    }

    private void RecordMemoryPhysicalMetric(long memoryValue)
    {
        var unscopedMemoryPhysicalMetric = _metricBuilder.TryBuildMemoryPhysicalMetric(memoryValue);
        RecordMetric(unscopedMemoryPhysicalMetric);
    }

    private void RecordMemoryWorkingSetMetric(long memoryValue)
    {
        var unscopedMemoryPhysicalMetric = _metricBuilder.TryBuildMemoryWorkingSetMetric(memoryValue);
        RecordMetric(unscopedMemoryPhysicalMetric);
    }

    private void RecordMetric(MetricWireModel metric)
    {
        if (metric == null)
            return;

        _metricAggregator.Collect(metric);
    }
}
