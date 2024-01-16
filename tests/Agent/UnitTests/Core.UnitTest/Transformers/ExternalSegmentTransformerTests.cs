// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class ExternalSegmentTransformerTests
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

            const string host = "www.foo.com";
            var uri = $"http://{host}/bar";
            const string method = "GET";

            var segment = GetSegment(uri, method);

            //make sure it does not throw
            segment.AddMetricStats(null, _configurationService);


        }

        [Test]
        public void TransformSegment_CreatesWebSegmentMetrics()
        {
            const string host = "www.foo.com";
            var uri = $"http://{host}/bar";
            const string method = "GET";

            var segment = GetSegment(uri, method, 5);


            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(4, unscoped.Count);

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/allWeb"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/www.foo.com/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey(segmentMetric));

            ClassicAssert.IsTrue(scoped.ContainsKey(segmentMetric));

            var data = scoped[segmentMetric];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(5, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allWeb", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }
        }

        [Test]
        public void TransformSegment_CreatesOtherSegmentMetrics()
        {
            const string host = "www.foo.com";
            var uri = $"http://{host}/bar";
            const string method = "GET";

            var segment = GetSegment(uri, method, 5);


            var txName = new TransactionMetricName("OtherTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(4, unscoped.Count);

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/allOther"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/www.foo.com/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey(segmentMetric));

            ClassicAssert.IsTrue(scoped.ContainsKey(segmentMetric));

            var data = scoped[segmentMetric];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(5, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }
        }

        [Test]
        public void TransformSegment_CreatesOtherWithCat()
        {
            const string host = "www.bar.com";
            var uri = $"http://{host}/foo";
            const string method = "GET";
            var externalCrossProcessId = "cpId";
            var externalTransactionName = "name";
            var catResponseData = new CrossApplicationResponseData(externalCrossProcessId, externalTransactionName, 1.1f, 2.2f, 3, "guid", false);

            var segment = GetSegment(uri, method, 5, catResponseData);


            var txName = new TransactionMetricName("OtherTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(6, unscoped.Count);

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/name";

            ClassicAssert.IsTrue(unscoped.ContainsKey("External/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/allOther"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/www.bar.com/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey(segmentMetric));
            ClassicAssert.IsTrue(unscoped.ContainsKey(txMetric));
            ClassicAssert.IsTrue(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"));

            ClassicAssert.IsFalse(scoped.ContainsKey(segmentMetric));
            ClassicAssert.IsTrue(scoped.ContainsKey(txMetric));

            var data = scoped[txMetric];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(5, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.bar.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric, txMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                ClassicAssert.AreEqual(1, data.Value0);
                ClassicAssert.AreEqual(5, data.Value1);
                ClassicAssert.AreEqual(5, data.Value2);
                ClassicAssert.AreEqual(5, data.Value3);
                ClassicAssert.AreEqual(5, data.Value4);
            }

            data = unscoped["ExternalApp/www.bar.com/cpId/all"];
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(5, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);
        }

        [Test]
        public void TransformSegment_CreatesWebWithCat()
        {
            const string host = "www.bar.com";
            var uri = $"http://{host}/foo";
            const string method = "GET";
            var externalCrossProcessId = "cpId";
            var externalTransactionName = "otherTxName";
            var catResponseData = new CrossApplicationResponseData(externalCrossProcessId, externalTransactionName, 1.1f, 2.2f, 3, "guid", false);

            var segment = GetSegment(uri, method, catResponseData);


            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            ClassicAssert.AreEqual(1, scoped.Count);
            ClassicAssert.AreEqual(6, unscoped.Count);

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/otherTxName";

            ClassicAssert.IsTrue(unscoped.ContainsKey("External/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/allWeb"));
            ClassicAssert.IsTrue(unscoped.ContainsKey("External/www.bar.com/all"));
            ClassicAssert.IsTrue(unscoped.ContainsKey(segmentMetric));
            ClassicAssert.IsTrue(unscoped.ContainsKey(txMetric));
            ClassicAssert.IsTrue(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"));

            ClassicAssert.IsFalse(scoped.ContainsKey(segmentMetric));
            ClassicAssert.IsTrue(scoped.ContainsKey(txMetric));

            var nameScoped = scoped[txMetric];
            var nameUnscoped = unscoped[txMetric];

            ClassicAssert.AreEqual(1, nameScoped.Value0);
            ClassicAssert.AreEqual(1, nameUnscoped.Value0);
        }

        #endregion Transform


        #region GetTransactionTraceName

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName_IfNoCatResponse()
        {
            const string host = "www.foo.com";
            var uri = $"http://{host}/bar";
            const string method = "GET";
            var segment = GetSegment(uri, method, null);

            var transactionTraceName = segment.GetTransactionTraceName();

            ClassicAssert.AreEqual("External/www.foo.com/Stream/GET", transactionTraceName);
        }

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName_IfCatResponse()
        {
            const string host = "www.foo.com";
            var uri = $"http://{host}/bar";
            const string method = "GET";
            var catResponseData = new CrossApplicationResponseData("cpId", "trxName", 1.1f, 2.2f, 3, "guid", false);
            var segment = GetSegment(uri, method, catResponseData);

            var transactionTraceName = segment.GetTransactionTraceName();

            ClassicAssert.AreEqual("ExternalTransaction/www.foo.com/cpId/trxName", transactionTraceName);
        }

        #endregion GetTransactionTraceName

        private static Segment GetSegment(string uri, string method, CrossApplicationResponseData catResponseData = null)
        {
            var data = new ExternalSegmentData(new Uri(uri), method, catResponseData);
            var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(data);
            builder.End();
            return builder;
        }

        private static Segment GetSegment(string uri, string method, double duration, CrossApplicationResponseData catResponseData = null)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);

            var data = new ExternalSegmentData(new Uri(uri), method, catResponseData);
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), methodCallData);
            segment.SetSegmentData(data);

            return new Segment(new TimeSpan(), TimeSpan.FromSeconds(duration), segment, null);
        }

    }
}
