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

        private void TransformSegment_AddParameter()
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(1));
            });

            const string metricName = "DotNet/name";
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
            var segment = GetSegment(name);

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);
            segment.AddMetricStats(txStats, _configurationService);
            segment.AddMetricStats(txStats, _configurationService);

            var scoped = txStats.GetScopedForTesting();
            var unscoped = txStats.GetUnscopedForTesting();

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(1));
                Assert.That(unscoped, Has.Count.EqualTo(1));
            });

            const string metricName = "DotNet/name";
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

            Assert.Multiple(() =>
            {
                Assert.That(scoped, Has.Count.EqualTo(2));
                Assert.That(unscoped, Has.Count.EqualTo(2));
            });

            const string metricName = "DotNet/name";
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

            const string metricName1 = "DotNet/otherName";
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

        [Test]
        public void GetTransactionTraceName_ReturnsCorrectName()
        {
            const string name = "name";
            var segment = GetSegment(name);

            var transactionTraceName = segment.GetTransactionTraceName();

            Assert.That(transactionTraceName, Is.EqualTo("name"));
        }

        #endregion GetTransactionTraceName

        private static Segment GetSegment(string name)
        {
            var builder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            builder.SetSegmentData(new SimpleSegmentData(name));
            builder.End();
            return builder;
        }

        private static Segment GetSegment(string name, double duration, TimeSpan start = new TimeSpan())
        {
            return new Segment(start, TimeSpan.FromSeconds(duration), GetSegment(name), null);
        }
    }
}
