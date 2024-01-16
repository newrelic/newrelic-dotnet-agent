// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
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
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("IAmRenamed", current.MetricNameModel.Name);
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        #endregion MergeUnscopedStats (PreCreated)

        #region MergeUnscopedStats (NotCreated)

        public void MergeUnscopedNotCreated_OneStat()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_VerifyRenaming()
        {
            IMetricNameService mNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => mNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "IAmRenamed");
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            var scopedStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            scopedStats[metric1.MetricNameModel.Name] = metric1.DataModel;
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, scopedStats);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("IAmRenamed", current.MetricNameModel.Name);
                ClassicAssert.AreEqual("myscope", current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopedNotCreated_OneStatEmptyString()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopedNotCreated_TwoStatsSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(2, current.DataModel.Value0);
                ClassicAssert.AreEqual(6, current.DataModel.Value1);
                ClassicAssert.AreEqual(4, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);

            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            collection.MergeUnscopedStats(metric2.MetricNameModel.Name, metric2.DataModel);
            collection.MergeUnscopedStats(metric2.MetricNameModel.Name, metric2.DataModel);
            stats = collection.ConvertToJsonForSending(_metricNameService);

            count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(4, current.DataModel.Value0);
                ClassicAssert.AreEqual(16, current.DataModel.Value1);
                ClassicAssert.AreEqual(12, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeUnscopeNotCreated_TwoDifferentSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
            collection.MergeUnscopedStats(metric2.MetricNameModel.Name, metric2.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricNameModel.Name.Equals("DotNet/name"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(5, current.DataModel.Value1);
                    ClassicAssert.AreEqual(4, current.DataModel.Value2);
                }
                else if (current.MetricNameModel.Name.Equals("DotNet/another"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(3, current.DataModel.Value1);
                    ClassicAssert.AreEqual(2, current.DataModel.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected Metric: " + current.MetricNameModel.Name);
                }
                ClassicAssert.AreEqual(null, current.MetricNameModel.Scope);

            }
            ClassicAssert.AreEqual(2, count);
        }

        #endregion MergeUnscopedStats (NotCreated)

        #region MergeScopedStats (String Scope Data)

        [Test]
        public void MergeScopedStats_OneStat_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("DotNet/name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual("myScope", current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSame_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual(metric1.MetricNameModel.Name, current.MetricNameModel.Name);
                ClassicAssert.AreEqual(metric1.MetricNameModel.Scope, current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(2, current.DataModel.Value0);
                ClassicAssert.AreEqual(6, current.DataModel.Value1);
                ClassicAssert.AreEqual(4, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoDifferentSame_StringData()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);
            collection.MergeScopedStats(metric2.MetricNameModel.Scope, metric2.MetricNameModel.Name, metric2.DataModel);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricNameModel.Name.Equals("DotNet/name"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(7, current.DataModel.Value1);
                    ClassicAssert.AreEqual(5, current.DataModel.Value2);
                }
                else if (current.MetricNameModel.Name.Equals("DotNet/another"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(3, current.DataModel.Value1);
                    ClassicAssert.AreEqual(2, current.DataModel.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
                }
                ClassicAssert.AreEqual(metric1.MetricNameModel.Scope, current.MetricNameModel.Scope);

            }
            ClassicAssert.AreEqual(2, count);
        }

        #endregion MergeScopedStats (String Scope Data)

        #region MergeScopedStats (SimpleStatsEngine)

        [Test]
        public void MergeScopedStats_OneStat()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, txStats);
            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual("myScope", current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(1, current.DataModel.Value0);
                ClassicAssert.AreEqual(3, current.DataModel.Value1);
                ClassicAssert.AreEqual(2, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            txStats.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, txStats);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual("myscope", current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(2, current.DataModel.Value0);
                ClassicAssert.AreEqual(6, current.DataModel.Value1);
                ClassicAssert.AreEqual(4, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoStatsSeparateEngines()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats1.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, txStats1);
            collection.MergeScopedStats(metric1.MetricNameModel.Scope, txStats2);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                ClassicAssert.AreEqual("name", current.MetricNameModel.Name);
                ClassicAssert.AreEqual("scope", current.MetricNameModel.Scope);
                ClassicAssert.AreEqual(2, current.DataModel.Value0);
                ClassicAssert.AreEqual(6, current.DataModel.Value1);
                ClassicAssert.AreEqual(4, current.DataModel.Value2);
            }
            ClassicAssert.AreEqual(1, count);
        }

        [Test]
        public void MergeScopedStats_TwoDifferentSame()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/another", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

            var collection = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> txStats1 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats1.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric2.MetricNameModel.Name, metric2.DataModel, MetricDataWireModel.BuildAggregateData);
            collection.MergeScopedStats(metric2.MetricNameModel.Scope, txStats1);
            collection.MergeScopedStats(metric2.MetricNameModel.Scope, txStats2);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricNameModel.Name.Equals("DotNet/name"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(2, current.DataModel.Value1);
                    ClassicAssert.AreEqual(1, current.DataModel.Value2);
                }
                else if (current.MetricNameModel.Name.Equals("DotNet/another"))
                {
                    ClassicAssert.AreEqual(1, current.DataModel.Value0);
                    ClassicAssert.AreEqual(3, current.DataModel.Value1);
                    ClassicAssert.AreEqual(2, current.DataModel.Value2);
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
                }
                ClassicAssert.AreEqual("scope", current.MetricNameModel.Scope);
            }
            ClassicAssert.AreEqual(2, count);
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
            txStats1.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats2.Merge(metric2.MetricNameModel.Name, metric2.DataModel, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats3 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats3.Merge(metric3.MetricNameModel.Name, metric3.DataModel, MetricDataWireModel.BuildAggregateData);
            MetricStatsDictionary<string, MetricDataWireModel> txStats4 = new MetricStatsDictionary<string, MetricDataWireModel>();
            txStats4.Merge(metric4.MetricNameModel.Name, metric4.DataModel, MetricDataWireModel.BuildAggregateData);

            collection.MergeScopedStats(metric2.MetricNameModel.Scope, txStats1);
            collection.MergeScopedStats(metric2.MetricNameModel.Scope, txStats2);
            collection.MergeScopedStats(metric3.MetricNameModel.Scope, txStats3);
            collection.MergeScopedStats(metric4.MetricNameModel.Scope, txStats4);

            IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
            var count = 0;

            foreach (MetricWireModel current in stats)
            {
                count++;
                if (current.MetricNameModel.Name.Equals("DotNet/name"))
                {
                    if (current.MetricNameModel.Scope.Equals("scope"))
                    {
                        ClassicAssert.AreEqual(1, current.DataModel.Value0);
                        ClassicAssert.AreEqual(2, current.DataModel.Value1);
                        ClassicAssert.AreEqual(1, current.DataModel.Value2);
                    }
                    else
                    {
                        ClassicAssert.AreEqual("myotherscope", current.MetricNameModel.Scope);
                        ClassicAssert.AreEqual(1, current.DataModel.Value0);
                        ClassicAssert.AreEqual(5, current.DataModel.Value1);
                        ClassicAssert.AreEqual(4, current.DataModel.Value2);
                    }
                }
                else if (current.MetricNameModel.Name.Equals("DotNet/another"))
                {
                    if (current.MetricNameModel.Scope.Equals("scope"))
                    {
                        ClassicAssert.AreEqual(1, current.DataModel.Value0);
                        ClassicAssert.AreEqual(3, current.DataModel.Value1);
                        ClassicAssert.AreEqual(2, current.DataModel.Value2);
                    }
                    else
                    {
                        ClassicAssert.AreEqual("myotherscope", current.MetricNameModel.Scope);
                        ClassicAssert.AreEqual(1, current.DataModel.Value0);
                        ClassicAssert.AreEqual(7, current.DataModel.Value1);
                        ClassicAssert.AreEqual(6, current.DataModel.Value2);
                    }
                }
                else
                {
                    Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
                }

            }
            ClassicAssert.AreEqual(4, count);
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
            collection1.MergeUnscopedStats(metric5.MetricNameModel.Name, metric5.DataModel);
            collection1.MergeScopedStats("collection1scope", scoped1);

            var collection2 = new MetricStatsCollection();
            MetricStatsDictionary<string, MetricDataWireModel> scoped2 = new MetricStatsDictionary<string, MetricDataWireModel>();
            scoped2.Merge("DotNet/name3", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1)), MetricDataWireModel.BuildAggregateData);
            scoped2.Merge("DotNet/name4", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)), MetricDataWireModel.BuildAggregateData);
            collection1.MergeUnscopedStats(metric6.MetricNameModel.Name, metric6.DataModel);
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
            ClassicAssert.AreEqual(6, count);

        }

        #endregion MergeStatsEngine

    }
}
