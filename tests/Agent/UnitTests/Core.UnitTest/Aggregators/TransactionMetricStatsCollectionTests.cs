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

        public static IMetricBuilder GetSimpleMetricBuilder()
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

            Assert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.AreEqual(1, unscoped.Count);
            Assert.AreEqual(0, scoped.Count);
            var data = unscoped["DotNet/name"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(3, data.Value1);
            Assert.AreEqual(2, data.Value2);
            Assert.AreEqual(3, data.Value3);
            Assert.AreEqual(3, data.Value4);
        }

        #endregion MergeUnscopedStats

        #region MergeScopedStats

        [Test]
        public void MergeScopedStats_One()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            Assert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.AreEqual(0, unscoped.Count);
            Assert.AreEqual(1, scoped.Count);
            var data = scoped["DotNet/name"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(3, data.Value1);
            Assert.AreEqual(2, data.Value2);
            Assert.AreEqual(3, data.Value3);
            Assert.AreEqual(3, data.Value4);
        }

        [Test]
        public void MergeScopedStats_Three()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/other"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3)));

            Assert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.AreEqual(0, unscoped.Count);
            Assert.AreEqual(2, scoped.Count);
            var data = scoped["DotNet/name"];
            Assert.NotNull(data);
            Assert.AreEqual(2, data.Value0);
            Assert.AreEqual(8, data.Value1);
            Assert.AreEqual(6, data.Value2);
            Assert.AreEqual(3, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = scoped["DotNet/other"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(4, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(4, data.Value3);
            Assert.AreEqual(4, data.Value4);
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

            Assert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            Assert.AreEqual(1, unscoped.Count);
            Assert.AreEqual(2, scoped.Count);
            var data = scoped["DotNet/name"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(3, data.Value1);
            Assert.AreEqual(2, data.Value2);
            Assert.AreEqual(3, data.Value3);
            Assert.AreEqual(3, data.Value4);

            data = unscoped["DotNet/name"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(5, data.Value1);
            Assert.AreEqual(4, data.Value2);
            Assert.AreEqual(5, data.Value3);
            Assert.AreEqual(5, data.Value4);

            data = scoped["DotNet/other"];
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(4, data.Value1);
            Assert.AreEqual(3, data.Value2);
            Assert.AreEqual(4, data.Value3);
            Assert.AreEqual(4, data.Value4);
        }

        #endregion Mixed

    }
}
