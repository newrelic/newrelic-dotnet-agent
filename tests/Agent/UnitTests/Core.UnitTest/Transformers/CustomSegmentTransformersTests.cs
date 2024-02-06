// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(1));
            });

            const string metricName = "Custom/name";
            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });
            var data = scoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = unscoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });

            var data = scoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(2));
                Assert.That(data.Value1, Is.EqualTo(10));
                Assert.That(data.Value2, Is.EqualTo(4));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = unscoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(2));
                Assert.That(data.Value1, Is.EqualTo(10));
                Assert.That(data.Value2, Is.EqualTo(4));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(2));
                Assert.That(unscoped, Has.Count.EqualTo(2));
            });

            const string metricName = "Custom/name";
            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName), Is.True);
                Assert.That(unscoped.ContainsKey(metricName), Is.True);
            });

            var data = scoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = unscoped[metricName];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            const string metricName1 = "Custom/otherName";
            Assert.Multiple(() =>
            {
                Assert.That(scoped.ContainsKey(metricName1), Is.True);
                Assert.That(unscoped.ContainsKey(metricName1), Is.True);
            });

            data = scoped[metricName1];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(6));
                Assert.That(data.Value2, Is.EqualTo(2));
                Assert.That(data.Value3, Is.EqualTo(6));
                Assert.That(data.Value4, Is.EqualTo(6));
            });

            data = unscoped[metricName1];
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(6));
                Assert.That(data.Value2, Is.EqualTo(2));
                Assert.That(data.Value3, Is.EqualTo(6));
                Assert.That(data.Value4, Is.EqualTo(6));
            });
        }

        #endregion Transform

        #region GetTransactionTraceName

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName()
        {
            const string name = "name";
            var segment = GetSegment(name, 5);

            var transactionTraceName = segment.GetTransactionTraceName();

            Assert.That(transactionTraceName, Is.EqualTo("name"));
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
