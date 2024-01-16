// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class SimpleSegmentTransformersTests
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
            const string name = "myname";
            var segment = GetSegment(name);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);
        }

        public void TransformSegment_AddParameter()
        {
            const string name = "myname";
            var segment = GetSegment(name);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);
        }

        [Test]
        public void TransformSegment_CreatesSegmentMetrics()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);
            segment.ChildFinished(GetSegment("kid", 2));

            TransactionMetricName txName = new TransactionMetricName("WebTransaction", "Test", false);
            TransactionMetricStatsCollection txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(1, unscoped.Count);

            const string metricName = "DotNet/name";
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
            var segment = GetSegment(name);

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(1, unscoped.Count);

            const string metricName = "DotNet/name";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            ClassicAssert.AreEqual(2, nameScoped.Value0);
            ClassicAssert.AreEqual(2, nameUnscoped.Value0);
        }

        [Test]
        public void TransformSegment_TwoTransformCallsDifferent()
        {
            const string name = "name";
            var segment = GetSegment(name);

            const string name1 = "otherName";
            var segment1 = GetSegment(name1);

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment1.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(2, scoped.Count);
            ClassicAssert.AreEqual(2, unscoped.Count);

            const string metricName = "DotNet/name";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName));

            var nameScoped = scoped[metricName];
            var nameUnscoped = unscoped[metricName];

            ClassicAssert.AreEqual(1, nameScoped.Value0);
            ClassicAssert.AreEqual(1, nameUnscoped.Value0);

            const string metricName1 = "DotNet/otherName";
            ClassicAssert.IsTrue(scoped.ContainsKey(metricName1));
            ClassicAssert.IsTrue(unscoped.ContainsKey(metricName1));

            nameScoped = scoped[metricName1];
            nameUnscoped = unscoped[metricName1];

            ClassicAssert.AreEqual(1, nameScoped.Value0);
            ClassicAssert.AreEqual(1, nameUnscoped.Value0);
        }

        #endregion Transform

        #region GetTransactionTraceName

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName()
        {
            const string name = "name";
            var segment = GetSegment(name);

            var transactionTraceName = segment.GetTransactionTraceName();

            ClassicAssert.AreEqual("name", transactionTraceName);
        }

        #endregion GetTransactionTraceName

        private static Segment GetSegment(string name)
        {
            var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new SimpleSegmentData(name));
            builder.End();
            return builder;
        }

        public static Segment GetSegment(string name, double duration, TimeSpan start = new TimeSpan())
        {
            return new Segment(start, TimeSpan.FromSeconds(duration), GetSegment(name), null);
        }
    }
}
