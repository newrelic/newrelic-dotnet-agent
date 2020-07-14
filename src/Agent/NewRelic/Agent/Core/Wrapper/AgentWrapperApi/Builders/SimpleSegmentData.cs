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
	public class SimpleSegmentData : AbstractSegmentData
	{
		[NotNull]
		private readonly String _name;

		public String Name => _name;

		public SimpleSegmentData(String name)
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

			MetricBuilder.TryBuildSimpleSegmentMetric(Name, duration, exclusiveDuration, txStats);
		}

		public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<string, object>> newParameters)
		{
			return new TypedSegment<SimpleSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
		}
	}
}
