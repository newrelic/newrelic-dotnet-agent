// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
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

            ClassicAssert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            ClassicAssert.AreEqual(1, unscoped.Count);
            ClassicAssert.AreEqual(0, scoped.Count);
            var data = unscoped["DotNet/name"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(3, data.Value1);
            ClassicAssert.AreEqual(2, data.Value2);
            ClassicAssert.AreEqual(3, data.Value3);
            ClassicAssert.AreEqual(3, data.Value4);
        }

        #endregion MergeUnscopedStats

        #region MergeScopedStats

        [Test]
        public void MergeScopedStats_One()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            ClassicAssert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            ClassicAssert.AreEqual(0, unscoped.Count);
            ClassicAssert.AreEqual(1, scoped.Count);
            var data = scoped["DotNet/name"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(3, data.Value1);
            ClassicAssert.AreEqual(2, data.Value2);
            ClassicAssert.AreEqual(3, data.Value3);
            ClassicAssert.AreEqual(3, data.Value4);
        }

        [Test]
        public void MergeScopedStats_Three()
        {

            var txName = new TransactionMetricName("WebTransaction", "Test", false);
            var txStats = new TransactionMetricStatsCollection(txName);

            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/name"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            txStats.MergeScopedStats(MetricName.Create("DotNet/other"), MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(3)));

            ClassicAssert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            ClassicAssert.AreEqual(0, unscoped.Count);
            ClassicAssert.AreEqual(2, scoped.Count);
            var data = scoped["DotNet/name"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(2, data.Value0);
            ClassicAssert.AreEqual(8, data.Value1);
            ClassicAssert.AreEqual(6, data.Value2);
            ClassicAssert.AreEqual(3, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            data = scoped["DotNet/other"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(4, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(4, data.Value3);
            ClassicAssert.AreEqual(4, data.Value4);
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

            ClassicAssert.AreEqual("WebTransaction/Test", txStats.GetTransactionName().PrefixedName);

            MetricStatsDictionary<string, MetricDataWireModel> unscoped = txStats.GetUnscopedForTesting();
            MetricStatsDictionary<string, MetricDataWireModel> scoped = txStats.GetScopedForTesting();
            ClassicAssert.AreEqual(1, unscoped.Count);
            ClassicAssert.AreEqual(2, scoped.Count);
            var data = scoped["DotNet/name"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(3, data.Value1);
            ClassicAssert.AreEqual(2, data.Value2);
            ClassicAssert.AreEqual(3, data.Value3);
            ClassicAssert.AreEqual(3, data.Value4);

            data = unscoped["DotNet/name"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(5, data.Value1);
            ClassicAssert.AreEqual(4, data.Value2);
            ClassicAssert.AreEqual(5, data.Value3);
            ClassicAssert.AreEqual(5, data.Value4);

            data = scoped["DotNet/other"];
            ClassicAssert.NotNull(data);
            ClassicAssert.AreEqual(1, data.Value0);
            ClassicAssert.AreEqual(4, data.Value1);
            ClassicAssert.AreEqual(3, data.Value2);
            ClassicAssert.AreEqual(4, data.Value3);
            ClassicAssert.AreEqual(4, data.Value4);
        }

        #endregion Mixed

    }
}
