﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Time;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class MessageBrokerSegmentData : AbstractSegmentData
	{

		private const String TransactionGuidSegmentParameterKey = "transaction_guid";

		public String Vendor { get; set; }

		public String Destination { get; set; }

		public MetricNames.MessageBrokerDestinationType DestinationType { get; set; }

		public MetricNames.MessageBrokerAction Action { get; set; }


		public MessageBrokerSegmentData(string vendor, string destination, MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action)
		{
			Vendor = vendor;
			Destination = destination;
			DestinationType = destinationType;
			Action = action;
		}

		public override bool IsCombinableWith(AbstractSegmentData otherData)
		{
			var otherTypedSegment = otherData as MessageBrokerSegmentData;
			if (otherTypedSegment == null)
				return false;

			if (!Vendor.Equals(otherTypedSegment.Vendor))
				return false;

			if (!Destination.Equals(otherTypedSegment.Destination))
				return false;

			if (DestinationType != otherTypedSegment.DestinationType)
				return false;

			if (Action != otherTypedSegment.Action)
				return false;

			return true;
		}

		public override string GetTransactionTraceName()
		{
			return MetricNames.GetMessageBroker(DestinationType, Action, Vendor, Destination).ToString();
		}

		public override void AddMetricStats(Segment segment, TimeSpan durationOfChildren, TransactionMetricStatsCollection txStats, IConfigurationService configService)
		{
			var duration = segment.Duration.Value;
			var exclusiveDuration = TimeSpanMath.Max(TimeSpan.Zero, duration - durationOfChildren);

			MetricBuilder.TryBuildMessageBrokerSegmentMetric(Vendor, Destination, DestinationType, Action, duration, exclusiveDuration, txStats);

		}

		public override Segment CreateSimilar(Segment segment, TimeSpan newRelativeStartTime, TimeSpan newDuration, [NotNull] IEnumerable<KeyValuePair<string, object>> newParameters)
		{
			return new TypedSegment<MessageBrokerSegmentData>(newRelativeStartTime, newDuration, segment, newParameters);
		}
	}
}
