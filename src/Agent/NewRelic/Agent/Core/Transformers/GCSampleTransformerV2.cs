// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers;

public interface IGCSampleTransformerV2
{
    void Transform(ImmutableGCSample sample);
}

public class GCSampleTransformerV2 : IGCSampleTransformerV2
{
    private readonly IMetricBuilder _metricBuilder;
    private readonly IMetricAggregator _metricAggregator;

    // public for testing purposes only
    public ImmutableGCSample PreviousSample { get; private set; }
    public ImmutableGCSample CurrentSample {get; private set;} = new();

    public GCSampleTransformerV2(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator)
    {
        _metricBuilder = metricBuilder;
        _metricAggregator = metricAggregator;
    }

    public void Transform(ImmutableGCSample sample)
    {
        PreviousSample = CurrentSample;
        CurrentSample = sample;

        var metrics = BuildMetrics();
        RecordMetrics(metrics);

    }

    private List<MetricWireModel> BuildMetrics()
    {
        var metrics = new List<MetricWireModel>
        {
            CreateMetric_ByteData(GCSampleType.Gen0Size, CurrentSample.GCHeapSizesBytes[0]),
            CreateMetric_Count(GCSampleType.Gen0CollectionCount, PreviousSample.GCCollectionCounts[0], CurrentSample.GCCollectionCounts[0]),
            CreateMetric_ByteData(GCSampleType.Gen0FragmentationSize, CurrentSample.GCFragmentationSizesBytes[0]),

            CreateMetric_ByteData(GCSampleType.Gen1Size, CurrentSample.GCHeapSizesBytes[1]),
            CreateMetric_Count(GCSampleType.Gen1CollectionCount, PreviousSample.GCCollectionCounts[1], CurrentSample.GCCollectionCounts[1]),
            CreateMetric_ByteData(GCSampleType.Gen1FragmentationSize, CurrentSample.GCFragmentationSizesBytes[1]),

            CreateMetric_ByteData(GCSampleType.Gen2Size, CurrentSample.GCHeapSizesBytes[2]),
            CreateMetric_Count(GCSampleType.Gen2CollectionCount, PreviousSample.GCCollectionCounts[2], CurrentSample.GCCollectionCounts[2]),
            CreateMetric_ByteData(GCSampleType.Gen2FragmentationSize, CurrentSample.GCFragmentationSizesBytes[2]),

            CreateMetric_ByteData(GCSampleType.LOHSize, CurrentSample.GCHeapSizesBytes[3]),
            CreateMetric_Count(GCSampleType.LOHCollectionCount, PreviousSample.GCCollectionCounts[3], CurrentSample.GCCollectionCounts[3]),
            CreateMetric_ByteData(GCSampleType.LOHFragmentationSize, CurrentSample.GCFragmentationSizesBytes[3]),

            CreateMetric_ByteData(GCSampleType.POHSize, CurrentSample.GCHeapSizesBytes[4]),
            CreateMetric_Count(GCSampleType.POHCollectionCount, PreviousSample.GCCollectionCounts[4], CurrentSample.GCCollectionCounts[4]),
            CreateMetric_ByteData(GCSampleType.POHFragmentationSize, CurrentSample.GCFragmentationSizesBytes[4]),

            CreateMetric_ByteData(GCSampleType.TotalHeapMemory, CurrentSample.TotalMemoryBytes),
            CreateMetric_ByteData(GCSampleType.TotalCommittedMemory, CurrentSample.TotalCommittedBytes),

            CreateMetric_ByteDataDelta(GCSampleType.TotalAllocatedMemory, PreviousSample.TotalAllocatedBytes, CurrentSample.TotalAllocatedBytes),
        };

        return metrics;
    }

    private void RecordMetrics(List<MetricWireModel> metrics)
    {
        foreach (var metric in metrics)
        {
            _metricAggregator.Collect(metric);
        }
    }

    /// <summary>
    /// Create a byte data metric representing the current value
    /// </summary>
    /// <param name="sampleType"></param>
    /// <param name="currentValueBytes"></param>
    /// <returns></returns>
    private MetricWireModel CreateMetric_ByteData(GCSampleType sampleType, long currentValueBytes)
    {
        return _metricBuilder.TryBuildGCBytesMetric(sampleType, currentValueBytes);
    }

    /// <summary>
    /// Create a byte data metric that is the difference between the current value and previous value
    /// </summary>
    /// <param name="sampleType"></param>
    /// <param name="previousValueBytes"></param>
    /// <param name="currentValueBytes"></param>
    /// <returns></returns>
    private MetricWireModel CreateMetric_ByteDataDelta(GCSampleType sampleType, long previousValueBytes, long currentValueBytes)
    {
        var sampleValueBytes = currentValueBytes - previousValueBytes;
        if (sampleValueBytes < 0)
        {
            sampleValueBytes = 0;
        }
        return _metricBuilder.TryBuildGCBytesMetric(sampleType, sampleValueBytes);
    }

    /// <summary>
    /// Create a count metric that is the difference between the current value and previous value
    /// </summary>
    /// <param name="sampleType"></param>
    /// <param name="previousValue"></param>
    /// <param name="currentValue"></param>
    /// <returns></returns>
    private MetricWireModel CreateMetric_Count(GCSampleType sampleType, long previousValue, long currentValue)
    {
        var sampleValue = currentValue - previousValue;
        if (sampleValue < 0)
        {
            sampleValue = 0;
        }

        return _metricBuilder.TryBuildGCCountMetric(sampleType, (int)sampleValue);
    }

}