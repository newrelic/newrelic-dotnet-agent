// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(4));
            });

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("External/all"), Is.True);
                Assert.That(unscoped.ContainsKey("External/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey("External/www.foo.com/all"), Is.True);
                Assert.That(unscoped.ContainsKey(segmentMetric), Is.True);

                Assert.That(scoped.ContainsKey(segmentMetric), Is.True);
            });

            var data = scoped[segmentMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allWeb", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(4));
            });

            const string segmentMetric = "External/www.foo.com/Stream/GET";
            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("External/all"), Is.True);
                Assert.That(unscoped.ContainsKey("External/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey("External/www.foo.com/all"), Is.True);
                Assert.That(unscoped.ContainsKey(segmentMetric), Is.True);

                Assert.That(scoped.ContainsKey(segmentMetric), Is.True);
            });

            var data = scoped[segmentMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.foo.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(6));
            });

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/name";

            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("External/all"), Is.True);
                Assert.That(unscoped.ContainsKey("External/allOther"), Is.True);
                Assert.That(unscoped.ContainsKey("External/www.bar.com/all"), Is.True);
                Assert.That(unscoped.ContainsKey(segmentMetric), Is.True);
                Assert.That(unscoped.ContainsKey(txMetric), Is.True);
                Assert.That(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"), Is.True);

                Assert.That(scoped.ContainsKey(segmentMetric), Is.False);
                Assert.That(scoped.ContainsKey(txMetric), Is.True);
            });

            var data = scoped[txMetric];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(5));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            var unscopedMetricsUsingDurationOnly = new string[] { "External/all", "External/allOther", "External/www.bar.com/all" };

            foreach (var current in unscopedMetricsUsingDurationOnly)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
            }

            var unscopedMetricsUsingExclusive = new string[] { segmentMetric, txMetric };

            foreach (var current in unscopedMetricsUsingExclusive)
            {
                data = unscoped[current];
                Assert.Multiple(() =>
                {
                    Assert.That(data.Value0, Is.EqualTo(1));
                    Assert.That(data.Value1, Is.EqualTo(5));
                    Assert.That(data.Value2, Is.EqualTo(5));
                    Assert.That(data.Value3, Is.EqualTo(5));
                    Assert.That(data.Value4, Is.EqualTo(5));
                });
            }

            data = unscoped["ExternalApp/www.bar.com/cpId/all"];
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(6));
            });

            const string segmentMetric = "External/www.bar.com/Stream/GET";
            const string txMetric = "ExternalTransaction/www.bar.com/cpId/otherTxName";

            Assert.Multiple(() =>
            {
                Assert.That(unscoped.ContainsKey("External/all"), Is.True);
                Assert.That(unscoped.ContainsKey("External/allWeb"), Is.True);
                Assert.That(unscoped.ContainsKey("External/www.bar.com/all"), Is.True);
                Assert.That(unscoped.ContainsKey(segmentMetric), Is.True);
                Assert.That(unscoped.ContainsKey(txMetric), Is.True);
                Assert.That(unscoped.ContainsKey("ExternalApp/www.bar.com/cpId/all"), Is.True);

                Assert.That(scoped.ContainsKey(segmentMetric), Is.False);
                Assert.That(scoped.ContainsKey(txMetric), Is.True);
            });

            var nameScoped = scoped[txMetric];
            var nameUnscoped = unscoped[txMetric];

            Assert.Multiple(() =>
            {
                Assert.That(nameScoped.Value0, Is.EqualTo(1));
                Assert.That(nameUnscoped.Value0, Is.EqualTo(1));
            });
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

            Assert.That(transactionTraceName, Is.EqualTo("External/www.foo.com/Stream/GET"));
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

            Assert.That(transactionTraceName, Is.EqualTo("ExternalTransaction/www.foo.com/cpId/trxName"));
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
