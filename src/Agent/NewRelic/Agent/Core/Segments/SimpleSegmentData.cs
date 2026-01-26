// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Time;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Segments;

public class SimpleSegmentData : AbstractSegmentData
{
    private readonly string _name;

    public string Name => _name;

    public SimpleSegmentData(string name)
    {
        _name = name;
    }

    public override bool IsCombinableWith(AbstractSegmentData otherData)
    {
        var otherTypedSegment = otherData as SimpleSegmentData;
        if (otherTypedSegment == null)
            return false;

        if (Name != otherTypedSegment.Name)
            return false;

        return true;
    }

    public override string GetTransactionTraceName()
    {
        return _name;
    }

    public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
    {
        var duration = segment.Duration.Value;
        var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);

        var name = Name;
        if (!string.IsNullOrWhiteSpace(segment.SegmentNameOverride))
        {
            name = segment.SegmentNameOverride;
        }

        MetricBuilder.TryBuildSimpleSegmentMetric(name, duration, exclusiveDuration, txStats);
    }
}
