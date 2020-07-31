// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Collections;
using NUnit.Framework;
using Telerik.JustMock;

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

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(4, unscoped.Count);

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            Assert.IsTrue(unscoped.ContainsKey("External/all"));
            Assert.IsTrue(unscoped.ContainsKey("External/allWeb"));
            Assert.IsTrue(unscoped.ContainsKey("External/www.foo.com/all"));
            Assert.IsTrue(unscoped.ContainsKey(segmentMetric));

            Assert.IsTrue(scoped.ContainsKey(segmentMetric));

            var data = scoped[segmentMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allWeb", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
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

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(4, unscoped.Count);

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            Assert.IsTrue(unscoped.ContainsKey("External/all"));
            Assert.IsTrue(unscoped.ContainsKey("External/allOther"));
            Assert.IsTrue(unscoped.ContainsKey("External/www.foo.com/all"));
            Assert.IsTrue(unscoped.ContainsKey(segmentMetric));

            Assert.IsTrue(scoped.ContainsKey(segmentMetric));

            var data = scoped[segmentMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
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

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(6, unscoped.Count);

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/name";

            Assert.IsTrue(unscoped.ContainsKey("External/all"));
            Assert.IsTrue(unscoped.ContainsKey("External/allOther"));
            Assert.IsTrue(unscoped.ContainsKey("External/www.bar.com/all"));
            Assert.IsTrue(unscoped.ContainsKey(segmentMetric));
            Assert.IsTrue(unscoped.ContainsKey(txMetric));
            Assert.IsTrue(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"));

            Assert.IsFalse(scoped.ContainsKey(segmentMetric));
            Assert.IsTrue(scoped.ContainsKey(txMetric));

            var data = scoped[txMetric];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.bar.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric, txMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.AreEqual(1, data.Value0);
                Assert.AreEqual(5, data.Value1);
                Assert.AreEqual(5, data.Value2);
                Assert.AreEqual(5, data.Value3);
                Assert.AreEqual(5, data.Value4);
            }

            data = unscoped["ExternalApp/www.bar.com/cpId/all"];
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(5, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);
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

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(6, unscoped.Count);

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/otherTxName";

            Assert.IsTrue(unscoped.ContainsKey("External/all"));
            Assert.IsTrue(unscoped.ContainsKey("External/allWeb"));
            Assert.IsTrue(unscoped.ContainsKey("External/www.bar.com/all"));
            Assert.IsTrue(unscoped.ContainsKey(segmentMetric));
            Assert.IsTrue(unscoped.ContainsKey(txMetric));
            Assert.IsTrue(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"));

            Assert.IsFalse(scoped.ContainsKey(segmentMetric));
            Assert.IsTrue(scoped.ContainsKey(txMetric));

            var nameScoped = scoped[txMetric];
            var nameUnscoped = unscoped[txMetric];

            Assert.AreEqual(1, nameScoped.Value0);
            Assert.AreEqual(1, nameUnscoped.Value0);
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

            Assert.AreEqual("External/www.foo.com/Stream/GET", transactionTraceName);
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

            Assert.AreEqual("ExternalTransaction/www.foo.com/cpId/trxName", transactionTraceName);
        }

        #endregion GetTransactionTraceName
        private static Segment GetSegment(string uri, string method, CrossApplicationResponseData catResponseData = null)
        {
            var data = new ExternalSegmentData(new Uri(uri), method, catResponseData);
            var builder = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), data);
            builder.End();
            return builder;
        }

        private static TypedSegment<ExternalSegmentData> GetSegment(string uri, string method, double duration, CrossApplicationResponseData catResponseData = null)
        {
            var methodCallData = new MethodCallData("foo", "bar", 1);
            var parameters = (new ConcurrentDictionary<string, object>());
            var myUri = new Uri(uri);

            var data = new ExternalSegmentData(new Uri(uri), method, catResponseData);

            return new TypedSegment<ExternalSegmentData>(new TimeSpan(), TimeSpan.FromSeconds(duration),
                new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), methodCallData, data, false));
        }

    }
}
