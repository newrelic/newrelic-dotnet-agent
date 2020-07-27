using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Collections;
using NUnit.Framework;
using Telerik.JustMock;

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
            const String vendor = "vendor1";
            const String destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);

        }

        [Test]
        public void TransformSegment_NullTransactionStats()
        {
            const String vendor = "vendor1";
            const String destination = "queueA";
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
            const String vendor = "vendor1";
            const String destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action, 5);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(1, unscoped.Count);

            const String metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";
            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            var data = scoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsSame()
        {
            const String vendor = "vendor1";
            const String destination = "queueA";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            const MetricNames.MessageBrokerAction action = MetricNames.MessageBrokerAction.Consume;
            var segment = GetSegment(vendor, destinationType, destination, action);
            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(1, unscoped.Count);
            const String metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";


            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            Assert.AreEqual(2, nameScoped.Value0);
            Assert.AreEqual(2, nameUnscoped.Value0);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsDifferent()
        {

            const String vendor = "vendor1";
            const String destination = "queueA";
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

            Assert.AreEqual(2, scoped.Count);
            Assert.AreEqual(2, unscoped.Count);

            const String metricName = "MessageBroker/vendor1/Queue/Consume/Named/queueA";

            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            Assert.AreEqual(1, nameScoped.Value0);
            Assert.AreEqual(1, nameUnscoped.Value0);

            const String metricName1 = "MessageBroker/vendor2/Queue/Consume/Named/myQueue";

            Assert.IsTrue(scoped.ContainsKey(metricName1));
            Assert.IsTrue(unscoped.ContainsKey(metricName1));

            nameScoped = scoped[metricName1];
            nameUnscoped = unscoped[metricName1];

            Assert.AreEqual(1, nameScoped.Value0);
            Assert.AreEqual(1, nameUnscoped.Value0);
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
            const String vendor = "MSMQ";
            const MetricNames.MessageBrokerDestinationType destinationType = MetricNames.MessageBrokerDestinationType.Queue;
            var segment = GetSegment(vendor, destinationType, queueName, action);
            var transactionTraceName = segment.GetTransactionTraceName();
            Assert.AreEqual(expectedResult, transactionTraceName);
        }

        #endregion GetTransactionTraceName
        private static Segment GetSegment(String vendor, MetricNames.MessageBrokerDestinationType destinationType, String destination, MetricNames.MessageBrokerAction action)
        {
            var timerFactory = Mock.Create<ITimerFactory>();
            var builder = new TypedSegment<MessageBrokerSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1),
                    new MessageBrokerSegmentData(vendor, destination, destinationType, action), false);
            builder.End();

            return builder;
        }

        private static TypedSegment<MessageBrokerSegmentData> GetSegment(String vendor, MetricNames.MessageBrokerDestinationType destinationType, String destination, MetricNames.MessageBrokerAction action, double duration)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var parameters = (new ConcurrentDictionary<String, Object>());
            return MessageBrokerSegmentDataTests.createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(duration), 2, 1, methodCallData, parameters, vendor, destination, destinationType, action, false);
        }
    }
}
