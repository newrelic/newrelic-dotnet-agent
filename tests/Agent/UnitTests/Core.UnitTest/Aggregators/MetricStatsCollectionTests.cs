// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators
{
    [TestFixture]
    class MetricStatsCollectionTests
    {
        private IMetricBuilder _metricBuilder;
        private IMetricNameService _metricNameService;

        [SetUp]
        public void SetUp()
        {
            _metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            _metricBuilder = new MetricWireModel.MetricBuilder(_metricNameService);
        }

        #region MergeUnscopedStats (PreCreated)

        [Test]
        public void MergeUnscopedStats_ChangeName()
        {
            IMetricNameService mNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => mNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "IAmRenamed");
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("IAmRenamed", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        #endregion MergeUnscopedStats (PreCreated)

        #region MergeUnscopedStats (NotCreated)

        public void MergeUnscopedNotCreated_OneStat()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_VerifyRenaming()
        {
            IMetricNameService mNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => mNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "IAmRenamed");
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            var scopedStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            scopedStats[metric1.MetricName.Name] = metric1.Data;
            collection.MergeScopedStats(metric1.MetricName.Scope, scopedStats);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("IAmRenamed", current.MetricName.Name);
                Assert.AreEqual("myscope", current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopedNotCreated_OneStatEmptyString()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopedNotCreated_TwoStatsSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                Assert.AreEqual(2, current.Data.Value0);
                Assert.AreEqual(6, current.Data.Value1);
                Assert.AreEqual(4, current.Data.Value2);
            }
            Assert.AreEqual(1, count);

            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            collection.MergeUnscopedStats(metric2.MetricName.Name, metric2.Data);
            collection.MergeUnscopedStats(metric2.MetricName.Name, metric2.Data);
            stats = collection.ConvertToJsonForSending(_metricNameService);

            count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                Assert.AreEqual(4, current.Data.Value0);
                Assert.AreEqual(16, current.Data.Value1);
                Assert.AreEqual(12, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopeNotCreated_TwoDifferentSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricName.Name, metric1.Data);
            collection.MergeUnscopedStats(metric2.MetricName.Name, metric2.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricName.Name.Equals("DotNet/name"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(5, current.Data.Value1);
                    Assert.AreEqual(4, current.Data.Value2);
                }
                else if (current.MetricName.Name.Equals("DotNet/another"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(3, current.Data.Value1);
                    Assert.AreEqual(2, current.Data.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected Metric: " + current.MetricName.Name);
                }
                Assert.AreEqual(null, current.MetricName.Scope);

            }
            Assert.AreEqual(2, count);
        }

        #endregion MergeUnscopedStats (NotCreated)

        #region MergeScopedStats (String Scope Data)

        [Test]
        public void MergeScopedStats_OneStat_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricName.Scope, metric1.MetricName.Name, metric1.Data);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("DotNet/name", current.MetricName.Name);
                Assert.AreEqual("myScope", current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSame_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricName.Scope, metric1.MetricName.Name, metric1.Data);
            collection.MergeScopedStats(metric1.MetricName.Scope, metric1.MetricName.Name, metric1.Data);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual(metric1.MetricName.Name, current.MetricName.Name);
                Assert.AreEqual(metric1.MetricName.Scope, current.MetricName.Scope);
                Assert.AreEqual(2, current.Data.Value0);
                Assert.AreEqual(6, current.Data.Value1);
                Assert.AreEqual(4, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoDifferentSame_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricName.Scope, metric1.MetricName.Name, metric1.Data);
            collection.MergeScopedStats(metric2.MetricName.Scope, metric2.MetricName.Name, metric2.Data);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricName.Name.Equals("DotNet/name"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(7, current.Data.Value1);
                    Assert.AreEqual(5, current.Data.Value2);
                }
                else if (current.MetricName.Name.Equals("DotNet/another"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(3, current.Data.Value1);
                    Assert.AreEqual(2, current.Data.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricName.Name);
                }
                Assert.AreEqual(metric1.MetricName.Scope, current.MetricName.Scope);

            }
            Assert.AreEqual(2, count);
        }

        #endregion MergeScopedStats (String Scope Data)

        #region MergeScopedStats (SimpleStatsEngine)

        [Test]
        public void MergeScopedStats_OneStat()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricName.Scope, txStats);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual("myScope", current.MetricName.Scope);
                Assert.AreEqual(1, current.Data.Value0);
                Assert.AreEqual(3, current.Data.Value1);
                Assert.AreEqual(2, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            txStats.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricName.Scope, txStats);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual("myscope", current.MetricName.Scope);
                Assert.AreEqual(2, current.Data.Value0);
                Assert.AreEqual(6, current.Data.Value1);
                Assert.AreEqual(4, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSeparateEngines()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats1.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricName.Scope, txStats1);
            collection.MergeScopedStats(metric1.MetricName.Scope, txStats2);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                Assert.AreEqual("name", current.MetricName.Name);
                Assert.AreEqual("scope", current.MetricName.Scope);
                Assert.AreEqual(2, current.Data.Value0);
                Assert.AreEqual(6, current.Data.Value1);
                Assert.AreEqual(4, current.Data.Value2);
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoDifferentSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats1.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric2.MetricName.Name, metric2.Data, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric2.MetricName.Scope, txStats1);
            collection.MergeScopedStats(metric2.MetricName.Scope, txStats2);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricName.Name.Equals("DotNet/name"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(2, current.Data.Value1);
                    Assert.AreEqual(1, current.Data.Value2);
                }
                else if (current.MetricName.Name.Equals("DotNet/another"))
                {
                    Assert.AreEqual(1, current.Data.Value0);
                    Assert.AreEqual(3, current.Data.Value1);
                    Assert.AreEqual(2, current.Data.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricName.Name);
                }
                Assert.AreEqual("scope", current.MetricName.Scope);
            }
            Assert.AreEqual(2, count);
        }

        [Test]
        public void MergeScopedStats_DifferentScopes()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var metric3 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "myotherscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            var metric4 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "myotherscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(6)));


            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats1.Merge(metric1.MetricName.Name, metric1.Data, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric2.MetricName.Name, metric2.Data, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats3 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats3.Merge(metric3.MetricName.Name, metric3.Data, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats4 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats4.Merge(metric4.MetricName.Name, metric4.Data, MetricDataWireModel.BuildAggregateData);

            collection.MergeScopedStats(metric2.MetricName.Scope, txStats1);
            collection.MergeScopedStats(metric2.MetricName.Scope, txStats2);
            collection.MergeScopedStats(metric3.MetricName.Scope, txStats3);
            collection.MergeScopedStats(metric4.MetricName.Scope, txStats4);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricName.Name.Equals("DotNet/name"))
                {
                    if (current.MetricName.Scope.Equals("scope"))
                    {
                        Assert.AreEqual(1, current.Data.Value0);
                        Assert.AreEqual(2, current.Data.Value1);
                        Assert.AreEqual(1, current.Data.Value2);
                    }
                    else
                    {
                        Assert.AreEqual("myotherscope", current.MetricName.Scope);
                        Assert.AreEqual(1, current.Data.Value0);
                        Assert.AreEqual(5, current.Data.Value1);
                        Assert.AreEqual(4, current.Data.Value2);
                    }
                }
                else if (current.MetricName.Name.Equals("DotNet/another"))
                {
                    if (current.MetricName.Scope.Equals("scope"))
                    {
                        Assert.AreEqual(1, current.Data.Value0);
                        Assert.AreEqual(3, current.Data.Value1);
                        Assert.AreEqual(2, current.Data.Value2);
                    }
                    else
                    {
                        Assert.AreEqual("myotherscope", current.MetricName.Scope);
                        Assert.AreEqual(1, current.Data.Value0);
                        Assert.AreEqual(7, current.Data.Value1);
                        Assert.AreEqual(6, current.Data.Value2);
                    }
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricName.Name);
                }

            }
            Assert.AreEqual(4, count);
        }

        #endregion MergeScopedStats (SimpleStatsEngine)

        #region MergeStatsEngine

        [Test]
        public void MergeStatsEngine_Mix()
        {
            var metric5 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
            var metric6 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(6)));

            var collection1 = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> scoped1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            scoped1.Merge("DotNet/name1", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)), MetricDataWireModel.BuildAggregateData);
            scoped1.Merge("DotNet/name2", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)), MetricDataWireModel.BuildAggregateData);
            collection1.MergeUnscopedStats(metric5.MetricName.Name, metric5.Data);
            collection1.MergeScopedStats("collection1scope", scoped1);

            var collection2 = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> scoped2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            scoped2.Merge("DotNet/name3", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1)), MetricDataWireModel.BuildAggregateData);
            scoped2.Merge("DotNet/name4", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)), MetricDataWireModel.BuildAggregateData);
            collection1.MergeUnscopedStats(metric6.MetricName.Name, metric6.Data);
            collection1.MergeScopedStats("collection2scope", scoped1);

            var collection3 = new MetricStatsCollection();
            collection3.Merge(collection1);
            collection3.Merge(collection2);

            IEnumerable<MetricWireModel> stats = collection3.ConvertToJsonForSending(_metricNameService);
            var count = 0;
            foreach (MetricWireModel current in stats)
            {
                count++;
            }
            Assert.AreEqual(6, count);

        }

        #endregion MergeStatsEngine

    }
}
