using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{

    public class MethodSegmentData : AbstractSegmentData
    {
        private readonly String _typeName;
        private readonly String _methodName;

        public String Type => _typeName;
        public String Method => _methodName;

        public MethodSegmentData(String typeName, String methodName)
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
            MetricBuilder.TryBuildMethodSegmentMetric(Type, Method, duration, exclusiveDuration, txStats);
        }

        public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, IEnumerable<KeyValuePair<string, object>> newParameters)
        {
            return new TypedSegment<MethodSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
        }
    }
}
