// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using System;
using NewRelic.Agent.Core.Metrics;
using System.Threading.Tasks;

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

            Assert.That(segment.ErrorData, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(segment.ErrorData.ErrorTypeName, Is.EqualTo("System.Exception"));
                Assert.That(segment.ErrorData.ErrorMessage, Is.EqualTo("Unhandled exception"));
            });
        }

        [Test]
        public void SetMessageBrokerDestination_SetsDestination_IfSegmentData_IsMessageBrokerSegmentData()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));
            var messageBrokerSegmentData = new MessageBrokerSegmentData("broker", "unknown", MetricNames.MessageBrokerDestinationType.Topic, MetricNames.MessageBrokerAction.Consume);
            segment.SetSegmentData(messageBrokerSegmentData);

            segment.SetMessageBrokerDestination("destination");

            Assert.That(((MessageBrokerSegmentData)segment.SegmentData).Destination, Is.EqualTo("destination"));
        }

        [Test]
        public void DurationOrZero_ReturnsZero_IfDurationIsNotSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));

            var duration = segment.DurationOrZero;

            Assert.That(duration, Is.EqualTo(TimeSpan.Zero));
        }
        [Test]
        public void DurationOrZero_ReturnsDuration_IfDurationIsSet()
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1), TimeSpan.Zero, TimeSpan.FromSeconds(1));

            var duration = segment.DurationOrZero;

            Assert.That(duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }
    }
}
