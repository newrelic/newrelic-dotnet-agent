// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using System;
using NewRelic.Agent.Core.Metrics;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class SegmentTests
    {
        [Test]
        public void End_WithException_HasErrorData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

            segment.End(new Exception("Unhandled exception"));

            Assert.IsNotNull(segment.ErrorData);
            Assert.AreEqual("System.Exception", segment.ErrorData.ErrorTypeName);
            Assert.AreEqual("Unhandled exception", segment.ErrorData.ErrorMessage);
        }

        [Test]
        public void SetMessageBrokerDestination_SetsDestination_IfSegmentData_IsMessageBrokerSegmentData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));
            var messageBrokerSegmentData = new MessageBrokerSegmentData("broker", "unknown", MetricNames.MessageBrokerDestinationType.Topic, MetricNames.MessageBrokerAction.Consume);
            segment.SetSegmentData(messageBrokerSegmentData);

            segment.SetMessageBrokerDestination("destination");

            Assert.AreEqual("destination", ((MessageBrokerSegmentData)segment.SegmentData).Destination );
        }
    }
}
