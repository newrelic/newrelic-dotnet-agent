// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class MessageBrokerTransformerTests
    {
        private IConfigurationService _configurationService;

        [SetUp]
        public void SetUp()
        {
            _configurationService = Mock.Create<IConfigurationService>();
        }

        #region Transform

        [Test]
        public void TransformSegment_NullStats()
        {
            const string vendor = "vendor1";
            const string destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);

        }

        [Test]
        public void TransformSegment_NullTransactionStats()
        {
            const string vendor = "vendor1";
            const string destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

        }

        [Test]
        public void TransformSegment_CreatesCustomSegmentMetrics()
        {
            const string vendor = "vendor1";
            const string destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action, 5);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(1));
            });

            const string metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";
            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            var data = scoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = unscoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });
        }

        [Test]
        public void TransformSegment_TwoTransformCallsSame()
        {
            const string vendor = "vendor1";
            const string destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(1));
            });
            const string metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";

            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            Assert.Multiple(() =>
            {
                Assert.That(nameScoped.Value0, Is.EqualTo(2));
                Assert.That(nameUnscoped.Value0, Is.EqualTo(2));
            });
        }

        [Test]
        public void TransformSegment_TwoTransformCallsDifferent()
        {

            const string vendor = "vendor1";
            const string destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);

            var segment1 = GetSegment("vendor2", destinationType, "myQueue", action);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment1.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(2));
                Assert.That(unscoped, Has.Count.EqualTo(2));
            });

            const string metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";

            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            Assert.Multiple(() =>
            {
                Assert.That(nameScoped.Value0, Is.EqualTo(1));
                Assert.That(nameUnscoped.Value0, Is.EqualTo(1));
            });

            const string metricName1 = "MessageBroker/vendor2/Queue/Consume/Named/myQueue";

            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName1), Is.True);
                Assert.That(unscoped.ContainsKey(metricName1), Is.True);
            });

            nameScoped = scoped[metricName1];
            nameUnscoped = unscoped[metricName1];

            Assert.Multiple(() =>
            {
                Assert.That(nameScoped.Value0, Is.EqualTo(1));
                Assert.That(nameUnscoped.Value0, Is.EqualTo(1));
            });
        }

        #endregion Transform

        #region GetTransactionTraceName

        [TestCase("apple", MetricNames.MessageBrokerAction.Peek, "MessageBroker/MSMQ/Queue/Peek/Named/apple")]
        [TestCase("apple", MetricNames.MessageBrokerAction.Purge, "MessageBroker/MSMQ/Queue/Purge/Named/apple")]
        [TestCase("apple", MetricNames.MessageBrokerAction.Consume, "MessageBroker/MSMQ/Queue/Consume/Named/apple")]
        [TestCase("apple", MetricNames.MessageBrokerAction.Produce, "MessageBroker/MSMQ/Queue/Produce/Named/apple")]
        [TestCase(null, MetricNames.MessageBrokerAction.Peek, "MessageBroker/MSMQ/Queue/Peek/Temp")]
        [TestCase(null, MetricNames.MessageBrokerAction.Purge, "MessageBroker/MSMQ/Queue/Purge/Temp")]
        [TestCase(null, MetricNames.MessageBrokerAction.Consume, "MessageBroker/MSMQ/Queue/Consume/Temp")]
        [TestCase(null, MetricNames.MessageBrokerAction.Produce, "MessageBroker/MSMQ/Queue/Produce/Temp")]
        public void GetTransactionTraceName_ReturnsCorrectName(string queueName, MetricNames.MessageBrokerAction action, string expectedResult)
        {
            const string vendor = "MSMQ";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            var segment = GetSegment(vendor, destinationType, queueName, action);
            var transactionTraceName = segment.GetTransactionTraceName();
            Assert.That(transactionTraceName, Is.EqualTo(expectedResult));
        }

        #endregion GetTransactionTraceName



        private static Segment GetSegment(string vendor, MetricNames.MessageBrokerDestinationType destinationType, string destination, MetricNames.MessageBrokerAction action)
        {
            var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new MessageBrokerSegmentData(vendor, destination, destinationType, action));
            builder.End();

            return builder;
        }

        private static Segment GetSegment(string vendor, MetricNames.MessageBrokerDestinationType destinationType, string destination, MetricNames.MessageBrokerAction action, double duration)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var parameters = (new Dictionary<string, object>());
            return MessageBrokerSegmentDataTestHelpers.CreateMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(duration), 2, 1, methodCallData, parameters, vendor, destination, destinationType, action, false);
        }
    }
}
