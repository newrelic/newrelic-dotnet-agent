// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Extensions.SystemExtensions;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels;

[JsonConverter(typeof(MetricDataWireModelJsonConverter))]
public class MetricDataWireModel
{
    private const string CannotBeNegative = "Cannot be negative";

    /// <summary>
    /// Count
    /// </summary>
    public readonly long Value0;

    /// <summary>
    /// Total Time
    /// </summary>
    public readonly float Value1;

    /// <summary>
    /// Exclusive Time
    /// </summary>
    public readonly float Value2;

    /// <summary>
    /// Max Time
    /// </summary>
    public readonly float Value3;

    /// <summary>
    /// Min Time
    /// </summary>
    public readonly float Value4;

    /// <summary>
    /// Sum Of Squares
    /// </summary>
    public readonly float Value5;

    private MetricDataWireModel(long value0, float value1, float value2, float value3, float value4, float value5)
    {
        Value0 = value0;
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Value4 = value4;
        Value5 = value5;
    }

    public static MetricDataWireModel BuildAggregateData(IEnumerable<MetricDataWireModel> metrics)
    {
        long value0 = 0;
        float value1 = 0, value2 = 0, value3 = float.MaxValue, value4 = float.MinValue, value5 = 0;

        foreach (var metric in metrics)
        {
            if (metric == null)
            {
                continue;
            }

            value0 += metric.Value0;
            value1 += metric.Value1;
            value2 += metric.Value2;
            value3 = Math.Min(value3, metric.Value3);
            value4 = Math.Max(value4, metric.Value4);
            value5 += metric.Value5;
        }

        return new MetricDataWireModel(value0, value1, value2, value3, value4, value5);
    }

    /// <summary>
    /// Aggregates two metric data wire models together. Always create a new one because
    /// we reuse some of the same wire models.
    /// </summary>
    /// <param name="metric0">Data to be aggregated.</param>
    /// <param name="metric1">Data to be aggregated.</param>
    /// <returns></returns>
    public static MetricDataWireModel BuildAggregateData(MetricDataWireModel metric0, MetricDataWireModel metric1)
    {
        return new MetricDataWireModel(
            (metric0.Value0 + metric1.Value0),
            (metric0.Value1 + metric1.Value1),
            (metric0.Value2 + metric1.Value2),
            (Math.Min(metric0.Value3, metric1.Value3)),
            (Math.Max(metric0.Value4, metric1.Value4)),
            (metric0.Value5 + metric1.Value5));
    }

    public static MetricDataWireModel BuildTimingData(TimeSpan totalTime, TimeSpan totalExclusiveTime)
    {
        if (totalTime.TotalSeconds < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(totalTime));
        }

        if (totalExclusiveTime.TotalSeconds < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(totalExclusiveTime));
        }

        return new MetricDataWireModel(1, (float)totalTime.TotalSeconds, (float)totalExclusiveTime.TotalSeconds, (float)totalTime.TotalSeconds, (float)totalTime.TotalSeconds, (float)totalTime.TotalSeconds * (float)totalTime.TotalSeconds);
    }

    public static MetricDataWireModel BuildCountData(long callCount = 1)
    {
        if (callCount < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(callCount));
        }

        return new MetricDataWireModel(callCount, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildDataUsageValue(long callCount, float dataSent, float dataReceived)
    {
        if (callCount < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(callCount));
        }

        if (dataSent < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(dataSent));
        }

        if (dataReceived < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(dataReceived));
        }

        return new MetricDataWireModel(callCount, dataSent, dataReceived, 0, 0, 0);
    }

    /// <summary>
    /// Allows recording of a metric based on a single observed value at point in time.
    /// </summary>
    /// <example>
    /// Current Speed, Current Temperature are good examples
    /// Number of Cache Entries, Number of Threads are also good examples
    /// # of ApiCalls in a harvest window is not a good example.
    /// </example>
    /// <param name="gaugeValue"></param>
    /// <returns></returns>
    public static MetricDataWireModel BuildGaugeValue(float gaugeValue)
    {
        return BuildSummaryValue(1, gaugeValue, gaugeValue, gaugeValue);
    }

    public static MetricDataWireModel BuildSummaryValue(int count, float value, float min, float max)
    {
        //Converts MELT style metrics to the current style of metrics
        return new MetricDataWireModel(count, value, value, min, max, value * value);
    }

    public static MetricDataWireModel BuildByteData(long totalBytes, long? exclusiveBytes = null)
    {
        exclusiveBytes = exclusiveBytes ?? totalBytes;
        if (totalBytes < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(totalBytes));
        }

        if (exclusiveBytes < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(exclusiveBytes));
        }

        const float bytesPerMb = 1048576f;
        var totalMegabytes = totalBytes / bytesPerMb;
        var totalExclusiveMegabytes = exclusiveBytes.Value / bytesPerMb;
        return new MetricDataWireModel(1, totalMegabytes, totalExclusiveMegabytes, totalMegabytes, totalMegabytes, totalMegabytes * totalMegabytes);
    }

    public static MetricDataWireModel BuildPercentageData(float percentage)
    {
        if (percentage < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(percentage));
        }

        return new MetricDataWireModel(1, percentage, percentage, percentage, percentage, percentage * percentage);
    }

    public static MetricDataWireModel BuildCpuTimeData(TimeSpan cpuTime)
    {
        if (cpuTime.TotalSeconds < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(cpuTime));
        }

        return new MetricDataWireModel(1, (float)cpuTime.TotalSeconds, (float)cpuTime.TotalSeconds, (float)cpuTime.TotalSeconds, (float)cpuTime.TotalSeconds, (float)cpuTime.TotalSeconds * (float)cpuTime.TotalSeconds);
    }

    public static MetricDataWireModel BuildApdexData(TimeSpan responseTime, TimeSpan apdexT)
    {
        if (responseTime.TotalSeconds < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(responseTime));
        }

        if (apdexT.TotalSeconds < 0)
        {
            throw new ArgumentException(CannotBeNegative, nameof(apdexT));
        }

        var apdexPerfZone = GetApdexPerfZone(responseTime, apdexT);
        var satisfying = apdexPerfZone == ApdexPerfZone.Satisfying ? 1 : 0;
        var tolerating = apdexPerfZone == ApdexPerfZone.Tolerating ? 1 : 0;
        var frustrating = apdexPerfZone == ApdexPerfZone.Frustrating ? 1 : 0;
        return new MetricDataWireModel(satisfying, tolerating, frustrating, (float)apdexT.TotalSeconds, (float)apdexT.TotalSeconds, 0);
    }

    public static MetricDataWireModel BuildFrustratedApdexData()
    {
        return new MetricDataWireModel(0, 0, 1, 0, 0, 0);
    }

    public static MetricDataWireModel BuildIfLinuxData(bool isLinux)
    {
        return new MetricDataWireModel(1, (isLinux ? 1 : 0), 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildBootIdError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    internal static MetricDataWireModel BuildKubernetesUsabilityError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildAwsUsabilityError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildAzureUsabilityError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildPcfUsabilityError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildGcpUsabilityError()
    {
        return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
    }

    public static MetricDataWireModel BuildAverageData(float value)
    {
        return new MetricDataWireModel(1, value, value, value, value, value * value);
    }

    private static ApdexPerfZone GetApdexPerfZone(TimeSpan responseTime, TimeSpan apdexT)
    {
        var ticks = responseTime.Ticks;
        if (ticks <= apdexT.Ticks)
        {
            return ApdexPerfZone.Satisfying;
        }

        return ticks <= apdexT.Multiply(4).Ticks ? ApdexPerfZone.Tolerating : ApdexPerfZone.Frustrating;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is MetricDataWireModel other &&
               this.Value0 == other.Value0 &&
               this.Value1 == other.Value1 &&
               this.Value2 == other.Value2 &&
               this.Value3 == other.Value3 &&
               this.Value4 == other.Value4 &&
               this.Value5 == other.Value5;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    private enum ApdexPerfZone
    {
        Satisfying,
        Tolerating,
        Frustrating
    }
}
