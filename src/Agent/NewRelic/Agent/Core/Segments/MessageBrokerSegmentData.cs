// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Segments
{
    public class MessageBrokerSegmentData : AbstractSegmentData
    {

        private const string TransactionGuidSegmentParameterKey = "transaction_guid";

        public string Vendor { get; set; }

        public string Destination { get; set; }

        public MetricNames.MessageBrokerDestinationType DestinationType { get; set; }

        public MetricNames.MessageBrokerAction Action { get; set; }

        public string MessagingSystemName {get; set;}
        public string CloudAccountId {get; set;}
        public string CloudRegion {get; set;}
        public string ServerAddress {get; set;}
        public int? ServerPort {get; set;}



        public MessageBrokerSegmentData(string vendor, string destination,
            MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action,
            string messagingSystemName = null, string cloudAccountId = null, string cloudRegion = null,
            string serverAddress = null, int? serverPort = null)
        {
            Vendor = vendor;
            Destination = destination;
            DestinationType = destinationType;
            Action = action;

            // attributes required for entity relationship mapping
            MessagingSystemName = messagingSystemName;
            CloudAccountId = cloudAccountId;
            CloudRegion = cloudRegion;
            ServerAddress = serverAddress;
            ServerPort = serverPort;
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

            if (MessagingSystemName != otherTypedSegment.MessagingSystemName)
                return false;

            if (CloudAccountId != otherTypedSegment.CloudAccountId)
                return false;

            if (CloudRegion != otherTypedSegment.CloudRegion)
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

        public override void SetSpanTypeSpecificAttributes(SpanAttributeValueCollection attribVals)
        {
            base.SetSpanTypeSpecificAttributes(attribVals);

            if (Action == MetricNames.MessageBrokerAction.Produce)
            {
                AttribDefs.SpanKind.TrySetValue(attribVals, "producer");
            }
            else if (Action == MetricNames.MessageBrokerAction.Consume)
            {
                AttribDefs.SpanKind.TrySetValue(attribVals, "consumer");
            }
            // else purge action - do not set the attribute

            if (!string.IsNullOrWhiteSpace(ServerAddress))
            {
                AttribDefs.BrokerServerAddress.TrySetValue(attribVals, ServerAddress);
            }

            if (ServerPort.HasValue)
            {
                AttribDefs.BrokerServerPort.TrySetValue(attribVals, ServerPort.Value);
            }
            
            AttribDefs.MessagingSystemName.TrySetValue(attribVals, MessagingSystemName);
            AttribDefs.MessagingDestinationName.TrySetValue(attribVals, Destination);
            AttribDefs.CloudRegion.TrySetValue(attribVals, CloudRegion);
            AttribDefs.CloudAccountId.TrySetValue(attribVals, CloudAccountId);
        }
    }
}
