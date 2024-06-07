// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using System;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    class TransactionMetricStatsCollectionTests
    {
        private IMetricBuilder _metricBuilder;

        [SetUp]
        public void SetUp()
        {
            _metricBuilder = GetSimpleMetricBuilder();
        }

        private static IMetricBuilder GetSimpleMetricBuilder()
        {
            var metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            return new MetricWireModel.MetricBuilder(metricNameService);
        }

        #region MergeUnscopedStats

        [Test]
        public void MergeUnscopedStats_One()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeUnscopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            Assert.That(txStats.GetTransactionName().PrefixedName, Is.EqualTo("WebTransaction/Test"));

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.Multiple(() =>
            {
                Assert.That(unscoped, Has.Count.EqualTo(1));
                Assert.That(scoped, Is.Empty);
            });
            var data = unscoped["DotNet/name"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(3));
                Assert.That(data.Value2, Is.EqualTo(2));
                Assert.That(data.Value3, Is.EqualTo(3));
                Assert.That(data.Value4, Is.EqualTo(3));
            });
        }

        #endregion MergeUnscopedStats

        #region MergeScopedStats

        [Test]
        public void MergeScopedStats_One()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            Assert.That(txStats.GetTransactionName().PrefixedName, Is.EqualTo("WebTransaction/Test"));

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.Multiple(() =>
            {
                Assert.That(unscoped, Is.Empty);
                Assert.That(scoped, Has.Count.EqualTo(1));
            });
            var data = scoped["DotNet/name"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(3));
                Assert.That(data.Value2, Is.EqualTo(2));
                Assert.That(data.Value3, Is.EqualTo(3));
                Assert.That(data.Value4, Is.EqualTo(3));
            });
        }

        [Test]
        public void MergeScopedStats_Three()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/other"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3)));

            Assert.That(txStats.GetTransactionName().PrefixedName, Is.EqualTo("WebTransaction/Test"));

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.Multiple(() =>
            {
                Assert.That(unscoped, Is.Empty);
                Assert.That(scoped, Has.Count.EqualTo(2));
            });
            var data = scoped["DotNet/name"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(2));
                Assert.That(data.Value1, Is.EqualTo(8));
                Assert.That(data.Value2, Is.EqualTo(6));
                Assert.That(data.Value3, Is.EqualTo(3));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = scoped["DotNet/other"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(4));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(4));
                Assert.That(data.Value4, Is.EqualTo(4));
            });
        }

        #endregion MergeScopedStats

        #region Mixed

        [Test]
        public void MergeStats_Three()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            txStats.MergeUnscopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/other"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3)));

            Assert.That(txStats.GetTransactionName().PrefixedName, Is.EqualTo("WebTransaction/Test"));

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.Multiple(() =>
            {
                Assert.That(unscoped, Has.Count.EqualTo(1));
                Assert.That(scoped, Has.Count.EqualTo(2));
            });
            var data = scoped["DotNet/name"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(3));
                Assert.That(data.Value2, Is.EqualTo(2));
                Assert.That(data.Value3, Is.EqualTo(3));
                Assert.That(data.Value4, Is.EqualTo(3));
            });

            data = unscoped["DotNet/name"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(5));
                Assert.That(data.Value2, Is.EqualTo(4));
                Assert.That(data.Value3, Is.EqualTo(5));
                Assert.That(data.Value4, Is.EqualTo(5));
            });

            data = scoped["DotNet/other"];
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(4));
                Assert.That(data.Value2, Is.EqualTo(3));
                Assert.That(data.Value3, Is.EqualTo(4));
                Assert.That(data.Value4, Is.EqualTo(4));
            });
        }

        #endregion Mixed

    }
}
