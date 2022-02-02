// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transactions
{
    [TestFixture]
    public class TransactionTests
    {
        private static DateTime Timestamp = DateTime.Now;
        private const string Level = "DEBUG";
        private const string Message = "the message";
        private const string SpanId = "span";
        private const string TraceId = "trace";

        private ITransactionSegmentState _transactionSegmentState;

        private IConfigurationService _configurationService;

        private IConfiguration _configuration;

        [SetUp]
        public void SetUp()
        {
            _configuration = GetDefaultConfiguration();
            _configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
            _transactionSegmentState = TransactionSegmentStateHelpers.GetItransactionSegmentState();

            Mock.Arrange(() => _transactionSegmentState.GetRelativeTime()).Returns(() => TimeSpan.Zero);
        }

        [Test]
        public void RecordLogMessage_SuccessfullyAddsMessage()
        {
            var node1 = GetNodeBuilder("seg1");
            var node2 = GetNodeBuilder("seg2");
            node1.Children.Add(node2);
            var node3 = GetNodeBuilder("seg3");
            var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment }, configurationService: _configurationService);
            transaction.RecordLogMessage(Timestamp, Level, Message, SpanId, TraceId);

            var logevent = transaction.LogEvents[0];
            Assert.AreEqual(1, transaction.LogEvents.Count);
            Assert.IsNotNull(logevent);
            Assert.AreEqual(Timestamp.ToUnixTimeMilliseconds(), logevent.TimeStamp);
            Assert.AreEqual(Level, logevent.Level);
            Assert.AreEqual(Message, logevent.Message);
            Assert.AreEqual(SpanId, logevent.SpanId);
            Assert.AreEqual(TraceId, logevent.TraceId);
        }

        [Test]
        public void RecordLogMessage_MissingLogLevel_DoesNotAdd()
        {
            var node1 = GetNodeBuilder("seg1");
            var node2 = GetNodeBuilder("seg2");
            node1.Children.Add(node2);
            var node3 = GetNodeBuilder("seg3");
            var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment }, configurationService: _configurationService);
            transaction.RecordLogMessage(Timestamp, null, Message, SpanId, TraceId);

            Assert.AreEqual(0, transaction.LogEvents.Count);
        }

        [Test]
        public void RecordLogMessage_Disabled_DoesNotAdd()
        {
            Mock.Arrange(() => _configuration.LogEventCollectorEnabled).Returns(false);

            var node1 = GetNodeBuilder("seg1");
            var node2 = GetNodeBuilder("seg2");
            node1.Children.Add(node2);
            var node3 = GetNodeBuilder("seg3");
            var transaction = TestTransactions.CreateDefaultTransaction(segments: new List<Segment>() { node1.Segment, node2.Segment, node3.Segment }, configurationService: _configurationService);
            transaction.RecordLogMessage(Timestamp, Level, Message, SpanId, TraceId);

            Assert.AreEqual(0, transaction.LogEvents.Count);
        }

        private SegmentTreeNodeBuilder GetNodeBuilder(string name, TimeSpan startTime = new TimeSpan(), TimeSpan duration = new TimeSpan())
        {
            return new SegmentTreeNodeBuilder(
                GetSegment(name, duration.TotalSeconds, startTime));
        }

        private Segment GetSegment(string name)
        {
            var builder = new Segment(_transactionSegmentState, new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new SimpleSegmentData(name));
            builder.End();
            return builder;
        }

        public Segment GetSegment(string name, double duration, TimeSpan start = new TimeSpan())
        {
            return new Segment(start, TimeSpan.FromSeconds(duration), GetSegment(name), null);
        }

        public static IConfiguration GetDefaultConfiguration()
        {
            return TestTransactions.GetDefaultConfiguration();
        }
    }
}
