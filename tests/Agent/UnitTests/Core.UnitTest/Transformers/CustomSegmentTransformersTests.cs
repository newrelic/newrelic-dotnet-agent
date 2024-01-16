// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class CustomSegmentTransformersTests
    {
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
            const string name = "name";
            var segment = GetSegment(name, 5);
            segment.AddMetricStats(null, _configurationService);

        }

        [Test]
        public void TransformSegment_CreatesCustomSegmentMetrics()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);


            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(1, unscoped.Count);

            const string metricName = "Custom/name";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName));
            var data = scoped[metricName];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsSame()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment.ChildFinished(GetSegment("kid", 2));
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            const string metricName = "Custom/name";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName));

            var data = scoped[metricName];
            ClassicAssert.AreEqual(2, data.Value0);
            ClassicAssert.AreEqual(10, data.Value1);
            ClassicAssert.AreEqual(4, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            ClassicAssert.AreEqual(2, data.Value0);
            ClassicAssert.AreEqual(10, data.Value1);
            ClassicAssert.AreEqual(4, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsDifferent()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));
            const string name1 = "otherName";
            var segment1 = GetSegment(name1, 6);
            segment1.ChildFinished(GetSegment("kid", 4));

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            segment1.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(2, scoped.Count);
            ClassicAssert.AreEqual(2, unscoped.Count);

            const string metricName = "Custom/name";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName));

            var data = scoped[metricName];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            data = unscoped[metricName];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            const string metricName1 = "Custom/otherName";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName1));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName1));

            data = scoped[metricName1];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(6, data.Value1);
            ClassicAssert.AreEqual(2, data.Value2);
            ClassicAssert.AreEqual(6, data.Value3);
            ClassicAssert.AreEqual(6, data.Value4);

            data = unscoped[metricName1];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(6, data.Value1);
            ClassicAssert.AreEqual(2, data.Value2);
            ClassicAssert.AreEqual(6, data.Value3);
            ClassicAssert.AreEqual(6, data.Value4);
        }

        #endregion Transform

        #region GetTransactionTraceName

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);

            var transactionTraceName = segment.GetTransactionTraceName();

            ClassicAssert.AreEqual("name", transactionTraceName);
        }

        #endregion GetTransactionTraceName

        private static Segment GetSegment(string name, double duration)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(new CustomSegmentData(name));

            return new Segment(new TimeSpan(), TimeSpan.FromSeconds(duration), segment, null);
        }
    }
}
