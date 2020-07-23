using System;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Collections;
using NUnit.Framework;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class CustomSegmentTransformersTests
    {
        [NotNull]
        private IConfigurationService _configurationService;

        [SetUp]
        public void SetUp()
        {
            _configurationService = Mock.Create<IConfigurationService>();
        }


        #region Transform

        [Test]
        public void TransformSegment_NullTransactionStats()
        {
            const String name = "name";
            var segment = GetSegment(name, 5);
            segment.AddMetricStats(null, _configurationService);

        }

        [Test]
        public void TransformSegment_CreatesCustomSegmentMetrics()
        {
            const String name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);


            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(1, unscoped.Count);

            const String metricName = "Custom/name";
            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));
            var data = scoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsSame()
        {
            const String name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment.ChildFinished(GetSegment("kid", 2));
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            const String metricName = "Custom/name";
            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var data = scoped[metricName];
            Assert.AreEqual(2, data.Value0);
            Assert.AreEqual(10, data.Value1);
            Assert.AreEqual(4, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            Assert.AreEqual(2, data.Value0);
            Assert.AreEqual(10, data.Value1);
            Assert.AreEqual(4, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsDifferent()
        {
            const String name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));
            const String name1 = "otherName";
            var segment1 = GetSegment(name1, 6);
            segment1.ChildFinished(GetSegment("kid", 4));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            segment1.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.AreEqual(2, scoped.Count);
            Assert.AreEqual(2, unscoped.Count);

            const String metricName = "Custom/name";
            Assert.IsTrue(scoped.ContainsKey(metricName));
            Assert.IsTrue(unscoped.ContainsKey(metricName));

            var data = scoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            const String metricName1 = "Custom/otherName";
            Assert.IsTrue(scoped.ContainsKey(metricName1));
            Assert.IsTrue(unscoped.ContainsKey(metricName1));

            data = scoped[metricName1];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(6, data.Value1);
            Assert.AreEqual(2, data.Value2);
            Assert.AreEqual(6, data.Value3);
            Assert.AreEqual(6, data.Value4);

            data = unscoped[metricName1];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(6, data.Value1);
            Assert.AreEqual(2, data.Value2);
            Assert.AreEqual(6, data.Value3);
            Assert.AreEqual(6, data.Value4);
        }

        #endregion Transform

        #region GetTransactionTraceName

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName()
        {
            const String name = "name";
            var segment = GetSegment(name, 5);

            var transactionTraceName = segment.GetTransactionTraceName();

            Assert.AreEqual("name", transactionTraceName);
        }

        #endregion GetTransactionTraceName

        private static TypedSegment<CustomSegmentData> GetSegment([NotNull] String name, double duration)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            return new TypedSegment<CustomSegmentData>(new TimeSpan(), TimeSpan.FromSeconds(duration),
                new TypedSegment<CustomSegmentData>(Mock.Create<ITransactionSegmentState>(), methodCallData, new CustomSegmentData(name), false));
        }
    }
}
