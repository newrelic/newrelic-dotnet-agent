// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Segments
{

    public class MethodSegmentData : AbstractSegmentData
    {
        private readonly string _typeName;
        private readonly string _methodName;

        public string Type => _typeName;
        public string Method => _methodName;

        public MethodSegmentData(string typeName, string methodName)
        {
            _typeName = typeName;
            _methodName = methodName;
        }

        public override bool IsCombinableWith(AbstractSegmentData otherData)
        {
            var otherTypedSegment = otherData as MethodSegmentData;
            if (otherTypedSegment == null)
                return false;

            if (Type != otherTypedSegment.Type)
                return false;

            if (Method != otherTypedSegment.Method)
                return false;

            return true;
        }

        public override string GetTransactionTraceName()
        {
            return MetricNames.GetDotNetInvocation(Type, Method).ToString();
        }

        public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
        {
            var duration = segment.Duration.Value;
            var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);
           
            if (!string.IsNullOrWhiteSpace(segment.SegmentNameOverride))
            {
                MetricBuilder.TryBuildSimpleSegmentMetric(segment.SegmentNameOverride, duration, exclusiveDuration, txStats);
            }
            else
            {
                MetricBuilder.TryBuildMethodSegmentMetric(Type, Method, duration, exclusiveDuration, txStats);
            }
            
        }
    }
}
