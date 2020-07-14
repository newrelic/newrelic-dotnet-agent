using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Time;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class CustomSegmentData : AbstractSegmentData
	{
		public String Name { get; }

		public CustomSegmentData(String name)
		{
			Name = name;
		}

		public override bool IsCombinableWith(AbstractSegmentData otherData)
		{
			var otherTypedSegment = otherData as CustomSegmentData;
			if (otherTypedSegment == null)
				return false;

			if (Name != otherTypedSegment.Name)
				return false;

			return true;
		}

		public override string GetTransactionTraceName()
		{
			return Name;
		}

		public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
		{
			var duration = segment.Duration.Value;
			var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);

			MetricBuilder.TryBuildCustomSegmentMetrics(Name, duration, exclusiveDuration, txStats);
		}

		public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<string, object>> newParameters)
		{
			return new TypedSegment<CustomSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
		}
	}
}

