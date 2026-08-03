// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Aggregators;

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
        _metricBuilder = new MetricWireModel.MetricBuilder();
    }

    [TearDown]
    public void TearDown()
    {
        _metricNameService.Dispose();
    }

    #region MergeUnscopedStats (PreCreated)

    [Test]
    public void MergeUnscopedStats_ChangeName()
    {
        IMetricNameService mNameService = Mock.Create<IMetricNameService>();
        Mock.Arrange(() => mNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "IAmRenamed");
        var metric1 = MetricWireModel.BuildMetric("name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("IAmRenamed"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    #endregion MergeUnscopedStats (PreCreated)

    #region MergeUnscopedStats (NotCreated)

    private void MergeUnscopedNotCreated_OneStat()
    {
        var metric1 = MetricWireModel.BuildMetric("name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_VerifyRenaming()
    {
        IMetricNameService mNameService = Mock.Create<IMetricNameService>();
        Mock.Arrange(() => mNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "IAmRenamed");
        var metric1 = MetricWireModel.BuildMetric("name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        var scopedStats = new MetricStatsDictionary<string, MetricDataWireModel>();
        scopedStats[metric1.MetricNameModel.Name] = metric1.DataModel;
        collection.MergeScopedStats(metric1.MetricNameModel.Scope, scopedStats);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(mNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("IAmRenamed"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myscope"));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeUnscopedNotCreated_OneStatEmptyString()
    {
        var metric1 = MetricWireModel.BuildMetric("name", "", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeUnscopedNotCreated_TwoStatsSame()
    {
        var metric1 = MetricWireModel.BuildMetric("name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
        collection.MergeUnscopedStats(metric1.MetricNameModel.Name, metric1.DataModel);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                Assert.That(current.DataModel.Value0, Is.EqualTo(2));
                Assert.That(current.DataModel.Value1, Is.EqualTo(6));
                Assert.That(current.DataModel.Value2, Is.EqualTo(4));
            });
        }
        Assert.That(count, Is.EqualTo(1));

        var metric2 = MetricWireModel.BuildMetric("name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
        collection.MergeUnscopedStats(metric2.MetricNameModel.Name, metric2.DataModel);
        collection.MergeUnscopedStats(metric2.MetricNameModel.Name, metric2.DataModel);
        stats = collection.ConvertToJsonForSending(_metricNameService);

        count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                Assert.That(current.DataModel.Value0, Is.EqualTo(4));
                Assert.That(current.DataModel.Value1, Is.EqualTo(16));
                Assert.That(current.DataModel.Value2, Is.EqualTo(12));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeUnscopeNotCreated_TwoDifferentSame()
    {
        var metric1 = MetricWireModel.BuildMetric("DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
        var metric2 = MetricWireModel.BuildMetric("DotNet/another", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

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
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(5));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(4));
                });
            }
            else if (current.MetricNameModel.Name.Equals("DotNet/another"))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(2));
                });
            }
            else
            {
                Assert.Fail("Unexpected Metric: " + current.MetricNameModel.Name);
            }
            Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));

        }
        Assert.That(count, Is.EqualTo(2));
    }

    #endregion MergeUnscopedStats (NotCreated)

    #region MergeScopedStats (String Scope Data)

    [Test]
    public void MergeScopedStats_OneStat_StringData()
    {
        var metric1 = MetricWireModel.BuildMetric("DotNet/name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("DotNet/name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myScope"));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_TwoStatsSame_StringData()
    {
        var metric1 = MetricWireModel.BuildMetric("name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);
        collection.MergeScopedStats(metric1.MetricNameModel.Scope, metric1.MetricNameModel.Name, metric1.DataModel);

        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo(metric1.MetricNameModel.Name));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo(metric1.MetricNameModel.Scope));
                Assert.That(current.DataModel.Value0, Is.EqualTo(2));
                Assert.That(current.DataModel.Value1, Is.EqualTo(6));
                Assert.That(current.DataModel.Value2, Is.EqualTo(4));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_TwoDifferentSame_StringData()
    {
        var metric1 = MetricWireModel.BuildMetric("DotNet/name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
        var metric2 = MetricWireModel.BuildMetric("DotNet/another", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

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
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(7));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(5));
                });
            }
            else if (current.MetricNameModel.Name.Equals("DotNet/another"))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(2));
                });
            }
            else
            {
                Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
            }
            Assert.That(current.MetricNameModel.Scope, Is.EqualTo(metric1.MetricNameModel.Scope));

        }
        Assert.That(count, Is.EqualTo(2));
    }

    #endregion MergeScopedStats (String Scope Data)

    #region MergeScopedStats (SimpleStatsEngine)

    [Test]
    public void MergeScopedStats_OneStat()
    {
        var metric1 = MetricWireModel.BuildMetric("name", "myScope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var collection = new MetricStatsCollection();
        MetricStatsDictionary<string, MetricDataWireModel> txStats = new MetricStatsDictionary<string, MetricDataWireModel>();
        txStats.Merge(metric1.MetricNameModel.Name, metric1.DataModel, MetricDataWireModel.BuildAggregateData);
        collection.MergeScopedStats(metric1.MetricNameModel.Scope, txStats);
        IEnumerable<MetricWireModel> stats = collection.ConvertToJsonForSending(_metricNameService);
        var count = 0;

        foreach (MetricWireModel current in stats)
        {
            count++;
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myScope"));
                Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                Assert.That(current.DataModel.Value2, Is.EqualTo(2));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_TwoStatsSame()
    {
        var metric1 = MetricWireModel.BuildMetric("name", "myscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
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
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myscope"));
                Assert.That(current.DataModel.Value0, Is.EqualTo(2));
                Assert.That(current.DataModel.Value1, Is.EqualTo(6));
                Assert.That(current.DataModel.Value2, Is.EqualTo(4));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_TwoStatsSeparateEngines()
    {
        var metric1 = MetricWireModel.BuildMetric("name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
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
            Assert.Multiple(() =>
            {
                Assert.That(current.MetricNameModel.Name, Is.EqualTo("name"));
                Assert.That(current.MetricNameModel.Scope, Is.EqualTo("scope"));
                Assert.That(current.DataModel.Value0, Is.EqualTo(2));
                Assert.That(current.DataModel.Value1, Is.EqualTo(6));
                Assert.That(current.DataModel.Value2, Is.EqualTo(4));
            });
        }
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void MergeScopedStats_TwoDifferentSame()
    {
        var metric1 = MetricWireModel.BuildMetric("DotNet/name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        var metric2 = MetricWireModel.BuildMetric("DotNet/another", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

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
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(2));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(1));
                });
            }
            else if (current.MetricNameModel.Name.Equals("DotNet/another"))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                    Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                    Assert.That(current.DataModel.Value2, Is.EqualTo(2));
                });
            }
            else
            {
                Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
            }
            Assert.That(current.MetricNameModel.Scope, Is.EqualTo("scope"));
        }
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void MergeScopedStats_DifferentScopes()
    {
        var metric1 = MetricWireModel.BuildMetric("DotNet/name", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        var metric2 = MetricWireModel.BuildMetric("DotNet/another", "scope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        var metric3 = MetricWireModel.BuildMetric("DotNet/name", "myotherscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));
        var metric4 = MetricWireModel.BuildMetric("DotNet/another", "myotherscope", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(6)));


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
                    Assert.Multiple(() =>
                    {
                        Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                        Assert.That(current.DataModel.Value1, Is.EqualTo(2));
                        Assert.That(current.DataModel.Value2, Is.EqualTo(1));
                    });
                }
                else
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myotherscope"));
                        Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                        Assert.That(current.DataModel.Value1, Is.EqualTo(5));
                        Assert.That(current.DataModel.Value2, Is.EqualTo(4));
                    });
                }
            }
            else if (current.MetricNameModel.Name.Equals("DotNet/another"))
            {
                if (current.MetricNameModel.Scope.Equals("scope"))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                        Assert.That(current.DataModel.Value1, Is.EqualTo(3));
                        Assert.That(current.DataModel.Value2, Is.EqualTo(2));
                    });
                }
                else
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(current.MetricNameModel.Scope, Is.EqualTo("myotherscope"));
                        Assert.That(current.DataModel.Value0, Is.EqualTo(1));
                        Assert.That(current.DataModel.Value1, Is.EqualTo(7));
                        Assert.That(current.DataModel.Value2, Is.EqualTo(6));
                    });
                }
            }
            else
            {
                Assert.Fail("Unexpected metric: " + current.MetricNameModel.Name);
            }

        }
        Assert.That(count, Is.EqualTo(4));
    }

    #endregion MergeScopedStats (SimpleStatsEngine)

    #region MergeStatsEngine

    [Test]
    public void MergeStatsEngine_Mix()
    {
        var metric5 = MetricWireModel.BuildMetric("DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)));
        var metric6 = MetricWireModel.BuildMetric("DotNet/another", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(6)));

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
        Assert.That(count, Is.EqualTo(6));

    }

    #endregion MergeStatsEngine

    #region Rename collisions

    /// <summary>
    /// Creates a name service that renames each key of <paramref name="renames"/> to its value and leaves
    /// every other metric name alone. A null value means the rename rules ignore that metric.
    /// </summary>
    private static IMetricNameService CreateRenamingMetricNameService(IDictionary<string, string> renames)
    {
        var nameService = Mock.Create<IMetricNameService>();
        Mock.Arrange(() => nameService.RenameMetric(Arg.IsAny<string>()))
            .Returns<string>(name => renames.TryGetValue(name, out var newName) ? newName : name);
        return nameService;
    }

    [Test]
    public void ConvertToJsonForSending_UnscopedMetricsCollidingAfterRename_AreMergedIntoOneMetric()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", "Custom/Renamed" },
            { "Custom/B", "Custom/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats("Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeUnscopedStats("Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("Custom/Renamed"));
            Assert.That(stats[0].MetricNameModel.Scope, Is.EqualTo(null));
            Assert.That(stats[0].DataModel.Value0, Is.EqualTo(15));
        });
    }

    [Test]
    public void ConvertToJsonForSending_UnscopedMetricsCollidingAfterRename_MergeTimingDataAcrossAllValues()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "DotNet/A", "DotNet/Renamed" },
            { "DotNet/B", "DotNet/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats("DotNet/A", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
        collection.MergeUnscopedStats("DotNet/B", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4)));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("DotNet/Renamed"));
            Assert.That(stats[0].DataModel.Value0, Is.EqualTo(2));
            Assert.That(stats[0].DataModel.Value1, Is.EqualTo(8));
            Assert.That(stats[0].DataModel.Value2, Is.EqualTo(6));
        });
    }

    [Test]
    public void ConvertToJsonForSending_ScopedMetricsInSameScopeCollidingAfterRename_AreMergedIntoOneMetric()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", "Custom/Renamed" },
            { "Custom/B", "Custom/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeScopedStats("myScope", "Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeScopedStats("myScope", "Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("Custom/Renamed"));
            Assert.That(stats[0].MetricNameModel.Scope, Is.EqualTo("myScope"));
            Assert.That(stats[0].DataModel.Value0, Is.EqualTo(15));
        });
    }

    [Test]
    public void ConvertToJsonForSending_ScopedMetricsInDifferentScopesCollidingAfterRename_AreNotMerged()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", "Custom/Renamed" },
            { "Custom/B", "Custom/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeScopedStats("scopeOne", "Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeScopedStats("scopeTwo", "Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(stats.Select(metric => metric.MetricNameModel.Name), Has.All.EqualTo("Custom/Renamed"));
            Assert.That(stats.First(metric => metric.MetricNameModel.Scope == "scopeOne").DataModel.Value0, Is.EqualTo(5));
            Assert.That(stats.First(metric => metric.MetricNameModel.Scope == "scopeTwo").DataModel.Value0, Is.EqualTo(10));
        });
    }

    [Test]
    public void ConvertToJsonForSending_UnscopedAndScopedMetricsWithTheSameRenamedName_AreNotMerged()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", "Custom/Renamed" },
            { "Custom/B", "Custom/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats("Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeScopedStats("myScope", "Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(stats.First(metric => metric.MetricNameModel.Scope == null).DataModel.Value0, Is.EqualTo(5));
            Assert.That(stats.First(metric => metric.MetricNameModel.Scope == "myScope").DataModel.Value0, Is.EqualTo(10));
        });
    }

    [Test]
    public void ConvertToJsonForSending_WhenOneOfTwoCollidingUnscopedMetricsIsIgnored_OnlyTheSurvivorsDataIsSent()
    {
        // A null rename means the rename rules say to ignore the metric
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", null },
            { "Custom/B", "Custom/Renamed" }
        });

        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats("Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeUnscopedStats("Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("Custom/Renamed"));
            Assert.That(stats[0].DataModel.Value0, Is.EqualTo(10));
        });
    }

    [Test]
    public void ConvertToJsonForSending_WhenAScopedMetricIsIgnored_ItIsNotSent()
    {
        var nameService = CreateRenamingMetricNameService(new Dictionary<string, string>
        {
            { "Custom/A", null }
        });

        var collection = new MetricStatsCollection();
        collection.MergeScopedStats("myScope", "Custom/A", MetricDataWireModel.BuildCountData(5));
        collection.MergeScopedStats("myScope", "Custom/B", MetricDataWireModel.BuildCountData(10));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("Custom/B"));
            Assert.That(stats[0].MetricNameModel.Scope, Is.EqualTo("myScope"));
            Assert.That(stats[0].DataModel.Value0, Is.EqualTo(10));
        });
    }

    [Test]
    public void ConvertToJsonForSending_AppliesRenameRulesExactlyOncePerMetricName()
    {
        // Guards against the metric name being run through the rename rules more than once, which
        // a non-idempotent rule (one that does not anchor on something it removes) would compound.
        var nameService = Mock.Create<IMetricNameService>();
        Mock.Arrange(() => nameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => "Prefix/" + name);

        var collection = new MetricStatsCollection();
        collection.MergeUnscopedStats("Custom/A", MetricDataWireModel.BuildCountData(5));

        var stats = collection.ConvertToJsonForSending(nameService);

        Assert.That(stats, Has.Count.EqualTo(1));
        Assert.That(stats[0].MetricNameModel.Name, Is.EqualTo("Prefix/Custom/A"));
    }

    #endregion Rename collisions

}