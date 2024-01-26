// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Core;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture]
    public class MetricWireModelTests
    {
        private readonly IMetricBuilder _metricBuilder = Utilities.GetSimpleMetricBuilder();

        private readonly IMetricNameService _metricNameService = Mock.Create<IMetricNameService>();

        [SetUp]
        public void SetUp()
        {
            Mock.Arrange(() => _metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);

        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _metricNameService.Dispose();
        }

        [Test]
        public void MetricWireModelStartsWithNameInString()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var wireModel = MetricWireModel.BuildMetric(_metricNameService, "originalName", null, metricData);

            var wireModelString = wireModel.ToString();

            Assert.That(wireModelString, Is.EqualTo("originalName ()NewRelic.Agent.Core.WireModels.MetricDataWireModel"));
        }

        [Test]
        public void ScopedMetricWireModelStartsWithNameInString()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var actual = MetricWireModel.BuildMetric(_metricNameService, "originalName", "theScope", metricData);

            // The code currently uses the type name of the data value and is not worth testing
            Assert.That(actual.ToString(), Does.StartWith("originalName (theScope)"));
        }

        [Test]
        public void MetricWireModelUsesReferenceEquals()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var metricWireModel = MetricWireModel.BuildMetric(_metricNameService, "originalName", "theScope", metricData);

#pragma warning disable NUnit2009 // The same value has been provided as both the actual and the expected argument
            Assert.That(metricWireModel, Is.EqualTo(metricWireModel));
#pragma warning restore NUnit2009 // The same value has been provided as both the actual and the expected argument
        }

        [TestCase("metric", "scope", 1, "metric", "scope", 1, ExpectedResult = true)]
        [TestCase("metric", "scope", 1, "metric", "scope", 2, ExpectedResult = false)]
        [TestCase("metric", "scope1", 1, "metric", "scope2", 1, ExpectedResult = false)]
        [TestCase("metric1", "scope", 1, "metric2", "scope", 1, ExpectedResult = false)]
        public bool MetricWireModelComparesItsData(string firstMetricName, string firstMetricScope, float firstMetricValue,
            string secondMetricName, string secondMetricScope, float secondMetricValue)
        {
            var first = MetricWireModel.BuildMetric(_metricNameService, firstMetricName, firstMetricScope, MetricDataWireModel.BuildGaugeValue(firstMetricValue));
            var second = MetricWireModel.BuildMetric(_metricNameService, secondMetricName, secondMetricScope, MetricDataWireModel.BuildGaugeValue(secondMetricValue));

            return first.Equals(second);
        }

        [Test]
        public void MetricWireModelDoesNotEqualNull()
        {
            var metricWireModel = MetricWireModel.BuildMetric(_metricNameService, "originalName", null, null);

            Assert.That(metricWireModel, Is.Not.EqualTo(null));
        }

        [TestCase("metric", "scope", 1, "metric", "scope", 1, ExpectedResult = false)]
        [TestCase("metric", "scope", 1, "metric", "scope", 2, ExpectedResult = false)]
        [TestCase("metric", "scope1", 1, "metric", "scope2", 1, ExpectedResult = false)]
        [TestCase("metric1", "scope", 1, "metric2", "scope", 1, ExpectedResult = false)]
        public bool MetricWireModelHashCodeUsesNameAndData(string firstMetricName, string firstMetricScope, float firstMetricValue,
            string secondMetricName, string secondMetricScope, float secondMetricValue)
        {
            var first = MetricWireModel.BuildMetric(_metricNameService, firstMetricName, firstMetricScope, MetricDataWireModel.BuildGaugeValue(firstMetricValue));
            var second = MetricWireModel.BuildMetric(_metricNameService, secondMetricName, secondMetricScope, MetricDataWireModel.BuildGaugeValue(secondMetricValue));

            return first.GetHashCode().Equals(second.GetHashCode());
        }

        #region AddMetricsToEngine

        [Test]
        public void AddMetricsToEngine_OneScopedMetric()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            Assert.That(metric1, Is.Not.Null);
            var data = metric1.DataModel;
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(3));
                Assert.That(data.Value2, Is.EqualTo(2));
            });

            var collection = new MetricStatsCollection();
            metric1.AddMetricsToCollection(collection);

            var actual = collection.ConvertToJsonForSending(_metricNameService);
            var unscopedCount = 0;
            var scopedCount = 0;
            var theScope = string.Empty;
            var metricName = string.Empty;
            MetricDataWireModel scopedData = null;
            foreach (var current in actual)
            {
                if (current.MetricNameModel.Scope == null)
                {
                    unscopedCount++;
                }
                else
                {
                    scopedCount++;
                    theScope = current.MetricNameModel.Scope;
                    metricName = current.MetricNameModel.Name;
                    scopedData = current.DataModel;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(scopedCount, Is.EqualTo(1));
                Assert.That(unscopedCount, Is.EqualTo(0));
                Assert.That(theScope, Is.EqualTo("scope"));
                Assert.That(metricName, Is.EqualTo("DotNet/name"));
            });
            Assert.That(scopedData, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(scopedData.Value0, Is.EqualTo(1));
                Assert.That(scopedData.Value1, Is.EqualTo(3));
                Assert.That(scopedData.Value2, Is.EqualTo(2));
            });
        }

        [TestCase("")]
        [TestCase(null)]
        public void AddMetricsToEngine_OneUnscopedMetricMissingScope(string empty)
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", empty,
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            Assert.That(metric1, Is.Not.Null);

            var data = metric1.DataModel;
            Assert.That(data, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(data.Value0, Is.EqualTo(1));
                Assert.That(data.Value1, Is.EqualTo(3));
                Assert.That(data.Value2, Is.EqualTo(2));
            });

            var collection = new MetricStatsCollection();
            metric1.AddMetricsToCollection(collection);

            var stats = collection.ConvertToJsonForSending(_metricNameService);

            foreach (var current in stats)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(current.MetricNameModel.Name, Is.EqualTo("DotNet/name"));
                    Assert.That(current.MetricNameModel.Scope, Is.EqualTo(null));
                });
                var myData = current.DataModel;
                Assert.Multiple(() =>
                {
                    Assert.That(myData.Value0, Is.EqualTo(1));
                    Assert.That(myData.Value1, Is.EqualTo(3));
                    Assert.That(myData.Value2, Is.EqualTo(2));
                });
            }
        }

        #endregion AddMetricsToEngine

        #region Merge

        [Test]
        public void Merge_MergesOneMetricCorrectly()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var mergedMetric = MetricWireModel.Merge(new[] { metric1 });

            NrAssert.Multiple(
                () => Assert.That(mergedMetric.MetricNameModel.Name, Is.EqualTo("DotNet/name")),
                () => Assert.That(mergedMetric.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(mergedMetric.DataModel.Value1, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value2, Is.EqualTo(1)),
                () => Assert.That(mergedMetric.DataModel.Value3, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value4, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value5, Is.EqualTo(9))
            );
        }

        [Test]
        public void Merge_MergesTwoMetricsCorrectly()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));

            var mergedMetric = MetricWireModel.Merge(metric1, metric2);

            NrAssert.Multiple(
                () => Assert.That(mergedMetric.MetricNameModel.Name, Is.EqualTo("DotNet/name")),
                () => Assert.That(mergedMetric.DataModel.Value0, Is.EqualTo(2)),
                () => Assert.That(mergedMetric.DataModel.Value1, Is.EqualTo(10)),
                () => Assert.That(mergedMetric.DataModel.Value2, Is.EqualTo(6)),
                () => Assert.That(mergedMetric.DataModel.Value3, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value4, Is.EqualTo(7)),
                () => Assert.That(mergedMetric.DataModel.Value5, Is.EqualTo(58))
            );
        }

        [Test]
        public void Merge_MergesThreeMetricsCorrectly()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric3 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(11)));

            var mergedMetric = MetricWireModel.Merge(new[] { metric1, metric2, metric3 });

            NrAssert.Multiple(
                () => Assert.That(mergedMetric.MetricNameModel.Name, Is.EqualTo("DotNet/name")),
                () => Assert.That(mergedMetric.DataModel.Value0, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value1, Is.EqualTo(23)),
                () => Assert.That(mergedMetric.DataModel.Value2, Is.EqualTo(17)),
                () => Assert.That(mergedMetric.DataModel.Value3, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value4, Is.EqualTo(13)),
                () => Assert.That(mergedMetric.DataModel.Value5, Is.EqualTo(227))
            );
        }

        [Test]
        public void Merge_MergesThreeMetricsCorrectly_WhenMergedProgressively()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric3 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(11)));

            var mergedMetric = MetricWireModel.Merge(new[] { metric1, metric2 });
            mergedMetric = MetricWireModel.Merge(new[] { mergedMetric, metric3 });

            NrAssert.Multiple(
                () => Assert.That(mergedMetric.MetricNameModel.Name, Is.EqualTo("DotNet/name")),
                () => Assert.That(mergedMetric.DataModel.Value0, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value1, Is.EqualTo(23)),
                () => Assert.That(mergedMetric.DataModel.Value2, Is.EqualTo(17)),
                () => Assert.That(mergedMetric.DataModel.Value3, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value4, Is.EqualTo(13)),
                () => Assert.That(mergedMetric.DataModel.Value5, Is.EqualTo(227))
            );
        }

        [Test]
        public void Merge_IgnoresNullMetrics()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));

            var mergedMetric = MetricWireModel.Merge(new[] { null, metric1, null });

            NrAssert.Multiple(
                () => Assert.That(mergedMetric.MetricNameModel.Name, Is.EqualTo("DotNet/name")),
                () => Assert.That(mergedMetric.DataModel.Value0, Is.EqualTo(1)),
                () => Assert.That(mergedMetric.DataModel.Value1, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value2, Is.EqualTo(1)),
                () => Assert.That(mergedMetric.DataModel.Value3, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value4, Is.EqualTo(3)),
                () => Assert.That(mergedMetric.DataModel.Value5, Is.EqualTo(9))
            );
        }

        [Test]
        public void Merge_Throws_IfGivenMetricsWithDifferentNames()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name1", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric3 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name2", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(11)));

            NrAssert.Throws<Exception>(() => MetricWireModel.Merge(new[] { metric1, metric2, metric3 }));
        }

        [Test]
        public void Merge_Throws_IfGivenMetricsWithDifferentScopes()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
            var metric2 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope1",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(5)));
            var metric3 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope2",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(11)));


            NrAssert.Throws<Exception>(() => MetricWireModel.Merge(new[] { metric1, metric2, metric3 }));
        }

        [Test]
        public void Merge_ThrowsIfAllNullsGiven()
        {
            NrAssert.Throws<Exception>(() => MetricWireModel.Merge(new MetricWireModel[] { null, null }));
        }

        [Test]
        public void Merge_ThrowsIfEmptyListGiven()
        {
            NrAssert.Throws<Exception>(() => MetricWireModel.Merge(new MetricWireModel[] { }));
        }

        #endregion Merge

        #region Serialization

        [Test]
        public void MetricWireModel_SerializesCorrectlyScoped()
        {
            const string expectedJson = @"[{""name"":""DotNet/name"",""scope"":""scope1""},[1,3.0,1.0,3.0,3.0,9.0]]";

            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope1", MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));

            var serializedMetric = JsonConvert.SerializeObject(metric1);

            Assert.That(serializedMetric, Is.EqualTo(expectedJson));
        }

        [Test]
        public void MetricWireModel_SerializesCorrectlyUnscoped()
        {
            const string expectedJson = @"[{""name"":""DotNet/name""},[1,3.0,1.0,3.0,3.0,9.0]]";

            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));

            var serializedMetric = JsonConvert.SerializeObject(metric1);

            Assert.That(serializedMetric, Is.EqualTo(expectedJson));
        }

        #endregion

        #region Build Metrics

        [Test]
        public void BuildMetricWireModelUsesOriginalName()
        {
            var actual = MetricWireModel.BuildMetric(_metricNameService, "originalName", null, null);
            Assert.That(actual.MetricNameModel.Name, Is.EqualTo("originalName"));
        }

        [Test]
        public void BuildMetricWireModelReturnsNullWhenNameIsNull()
        {
            var mockNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => mockNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(null);

            var actual = MetricWireModel.BuildMetric(mockNameService, "originalName", null, null);
            Assert.That(actual, Is.Null);
        }

        [Test]
        public void BuildAggregateData()
        {
            var one = MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4));
            var two = MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(2));
            var actual = MetricDataWireModel.BuildAggregateData(one, two);
            Assert.Multiple(() =>
            {
                Assert.That(actual.Value0, Is.EqualTo(2));
                Assert.That(actual.Value1, Is.EqualTo(12));
                Assert.That(actual.Value2, Is.EqualTo(6));
                Assert.That(actual.Value3, Is.EqualTo(5));
                Assert.That(actual.Value4, Is.EqualTo(7));
                Assert.That(actual.Value5, Is.EqualTo(one.Value5 + two.Value5));
            });
        }

        [Test]
        public void BuildGaugeMetric()
        {
            const int expectedValue = 21;

            var metricData = MetricDataWireModel.BuildGaugeValue(expectedValue);

            NrAssert.Multiple(
                () => Assert.That(metricData.Value0, Is.EqualTo(1)),
                () => Assert.That(metricData.Value1, Is.EqualTo(expectedValue)),
                () => Assert.That(metricData.Value2, Is.EqualTo(expectedValue)),
                () => Assert.That(metricData.Value3, Is.EqualTo(expectedValue)),
                () => Assert.That(metricData.Value4, Is.EqualTo(expectedValue)),
                () => Assert.That(metricData.Value5, Is.EqualTo(expectedValue * expectedValue))
            );
        }

        [Test]
        public void BuildSummaryMetric()
        {
            const int count = 18;
            const int value = 20;
            const int min = 10;
            const int max = 30;
            const int sumSquares = value * value;

            var metricData = MetricDataWireModel.BuildSummaryValue(count, value, min, max);

            NrAssert.Multiple(
                () => Assert.That(metricData.Value0, Is.EqualTo(count)),
                () => Assert.That(metricData.Value1, Is.EqualTo(value)),
                () => Assert.That(metricData.Value2, Is.EqualTo(value)),
                () => Assert.That(metricData.Value3, Is.EqualTo(min)),
                () => Assert.That(metricData.Value4, Is.EqualTo(max)),
                () => Assert.That(metricData.Value5, Is.EqualTo(sumSquares))
            );
        }

        [Test]
        public void BuildDataUsageMetric()
        {
            const int callCount = 1;
            const int dataSent = 20;
            const int dataReceived = 10;

            var metricData = MetricDataWireModel.BuildDataUsageValue(callCount, dataSent, dataReceived);

            NrAssert.Multiple(
                () => Assert.That(metricData.Value0, Is.EqualTo(callCount)),
                () => Assert.That(metricData.Value1, Is.EqualTo(dataSent)),
                () => Assert.That(metricData.Value2, Is.EqualTo(dataReceived))
            );
        }

        [TestCase(true, "CPU/WebTransaction")]
        [TestCase(false, "CPU/OtherTransaction")]
        public void BuildCpuTimeRollupMetric(bool isWebTransaction, string expectedMetricName)
        {
            var actual = _metricBuilder.TryBuildCpuTimeRollupMetric(isWebTransaction, TimeSpan.FromSeconds(2));

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo(expectedMetricName)),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value1, Is.EqualTo(2))
            );
        }

        [TestCase(Metrics.MetricNames.WebTransactionPrefix, "CPU/WebTransaction/transactionName")]
        [TestCase(Metrics.MetricNames.OtherTransactionPrefix, "CPU/OtherTransaction/transactionName")]
        public void BuildCpuTimeMetric(string transactionPrefix, string expectedMetricName)
        {
            var transactionMetricName = new TransactionMetricName(transactionPrefix, "transactionName");
            var actual = _metricBuilder.TryBuildCpuTimeMetric(transactionMetricName, TimeSpan.FromSeconds(2));

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo(expectedMetricName)),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value1, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildSupportabilityGaugeMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityGaugeMetric("metricName", 2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/metricName")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value1, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildDotnetCoreVersionMetric()
        {
            var actual = _metricBuilder.TryBuildDotnetCoreVersionMetric(DotnetCoreVersion.Other);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Dotnet/NetCore/Other")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildAgentVersionByHostMetric()
        {
            var actual = _metricBuilder.TryBuildAgentVersionByHostMetric("hostName", "10.0.0.0");

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/AgentVersion/hostName/10.0.0.0")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildMetricHarvestAttemptMetric()
        {
            var actual = _metricBuilder.TryBuildMetricHarvestAttemptMetric();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/MetricHarvest/transmit")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildTransactionEventReservoirResizedMetric()
        {
            var actual = _metricBuilder.TryBuildTransactionEventReservoirResizedMetric();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/AnalyticsEvents/TryResizeReservoir")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildTransactionEventsRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildTransactionEventsRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/AnalyticsEvents/TotalEventsRecollected")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildCustomEventReservoirResizedMetric()
        {
            var actual = _metricBuilder.TryBuildCustomEventReservoirResizedMetric();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Events/Customer/TryResizeReservoir")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildCustomEventsRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildCustomEventsRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Events/Customer/TotalEventsRecollected")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildErrorTracesRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildErrorTracesRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Errors/TotalErrorsRecollected")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildSqlTracesRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildSqlTracesRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/SqlTraces/TotalSqlTracesRecollected")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildFeatureEnabledMetric()
        {
            var actual = _metricBuilder.TryBuildFeatureEnabledMetric("featureName");

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/FeatureEnabled/featureName")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void BuildLinuxOsMetric(bool isLinux, float expectedCount)
        {
            var actual = _metricBuilder.TryBuildLinuxOsMetric(isLinux);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/OS/Linux")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value1, Is.EqualTo(expectedCount))
            );
        }

        [Test]
        public void BuildBootIdErrorMetric()
        {
            var actual = _metricBuilder.TryBuildBootIdError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/boot_id/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildKubernetesUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildKubernetesUsabilityError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/kubernetes/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildAwsUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildAwsUsabilityError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/aws/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildAzureUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildAzureUsabilityError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/azure/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildPcfUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildPcfUsabilityError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/pcf/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildGcpUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildGcpUsabilityError();

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/utilization/gcp/error")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildSupportabilityEndpointMethodErrorAttemptsMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityEndpointMethodErrorAttempts("endpoint");

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Agent/Collector/endpoint/Attempts")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(1))
            );
        }

        [Test]
        public void BuildSupportabilityPayloadsDroppedDueToMaxPayloadLimitMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit("endpoint", 2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/DotNet/Collector/MaxPayloadSizeLimit/endpoint")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildSupportabilityLoggingEventsDroppedMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityLoggingEventsDroppedMetric(2);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo("Supportability/Logging/Forwarding/Dropped")),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(2))
            );
        }

        [Test]
        public void BuildCountMetric()
        {
            const string metricName = "Some/Metric/Name";
            const int count = 999;

            var actual = _metricBuilder.TryBuildCountMetric(metricName, count);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo(metricName)),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel.Value0, Is.EqualTo(count))
            );
        }

        [Test]
        public void BuildByteMetric()
        {
            const string metricName = "Some/Metric/Name";
            const long byteCount = 1024 * 1024 * 1024;

            var actual = _metricBuilder.TryBuildByteMetric(metricName, byteCount);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo(metricName)),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel, Is.EqualTo(MetricDataWireModel.BuildByteData(byteCount)))
            );
        }

        [Test]
        public void BuildByteMetric_WithExclusiveBytes()
        {
            const string metricName = "Some/Metric/Name";
            const long totalBytes = 1024 * 1024 * 1024;
            const long exclusiveBytes = 1024 * 1024 * 128;

            var actual = _metricBuilder.TryBuildByteMetric(metricName, totalBytes, exclusiveBytes);

            NrAssert.Multiple(
                () => Assert.That(actual.MetricNameModel.Name, Is.EqualTo(metricName)),
                () => Assert.That(actual.MetricNameModel.Scope, Is.Null),
                () => Assert.That(actual.DataModel, Is.EqualTo(MetricDataWireModel.BuildByteData(totalBytes, exclusiveBytes)))
            );
        }

        #endregion

        #region DistributedTracing
        private static List<TestCaseData> GetSupportabilityDistributedTraceTestData()
        {
            return new List<TestCaseData>()
            {
                new TestCaseData( "TryBuildAcceptPayloadException", "Supportability/DistributedTrace/AcceptPayload/Exception" ),
                new TestCaseData( "TryBuildAcceptPayloadException", "Supportability/DistributedTrace/AcceptPayload/Exception"),
                new TestCaseData( "TryBuildAcceptPayloadParseException", "Supportability/DistributedTrace/AcceptPayload/ParseException"),
                new TestCaseData( "TryBuildAcceptPayloadIgnoredCreateBeforeAccept", "Supportability/DistributedTrace/AcceptPayload/Ignored/CreateBeforeAccept"),
                new TestCaseData( "TryBuildAcceptPayloadIgnoredMultiple", "Supportability/DistributedTrace/AcceptPayload/Ignored/Multiple"),
                new TestCaseData( "TryBuildAcceptPayloadIgnoredMajorVersion", "Supportability/DistributedTrace/AcceptPayload/Ignored/MajorVersion"),
                new TestCaseData( "TryBuildAcceptPayloadIgnoredNull", "Supportability/DistributedTrace/AcceptPayload/Ignored/Null"),
                new TestCaseData( "TryBuildCreatePayloadException", "Supportability/DistributedTrace/CreatePayload/Exception"),
                new TestCaseData( "TryBuildTraceContextAcceptException", "Supportability/TraceContext/Accept/Exception"),
                new TestCaseData( "TryBuildTraceContextTraceStateParseException", "Supportability/TraceContext/TraceState/Parse/Exception"),
                new TestCaseData( "TryBuildTraceContextCreateException", "Supportability/TraceContext/Create/Exception")
            };
        }

        private static List<TestCaseData> GetSupportabilityDistributedTraceTestData_SuccessMetric()
        {
            return new List<TestCaseData>()
            {
                new TestCaseData( "TryBuildAcceptPayloadSuccess", "Supportability/DistributedTrace/AcceptPayload/Success"),
                new TestCaseData( "TryBuildCreatePayloadSuccess", "Supportability/DistributedTrace/CreatePayload/Success"),
            };
        }

        [Test]
        [TestCaseSource(nameof(GetSupportabilityDistributedTraceTestData))]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDistributedTracing(string propertyName, string name)
        {
            var propertyInfo = _metricBuilder.GetType().GetProperty(propertyName);
            Assert.That(propertyInfo, Is.Not.Null);

            var obj = propertyInfo.GetValue(_metricBuilder);
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj, Is.InstanceOf(typeof(MetricWireModel)));

            var wireModel = obj as MetricWireModel;
            Assert.That(wireModel, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(wireModel.MetricNameModel.Name, Is.EqualTo(name));
                Assert.That(wireModel.DataModel.Value0, Is.EqualTo(1));
                Assert.That(wireModel.DataModel.Value1, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value2, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value3, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value4, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value5, Is.EqualTo(0));
            });
        }

        [Test]
        [TestCaseSource(nameof(GetSupportabilityDistributedTraceTestData_SuccessMetric))]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDistributedTracing_SuccessMetrics(string methodName, string name)
        {
            var methodInfo = _metricBuilder.GetType().GetMethod(methodName);
            Assert.That(methodInfo, Is.Not.Null);

            var obj = methodInfo.Invoke(_metricBuilder, new object[] { 2 });
            Assert.That(obj, Is.Not.Null);
            Assert.That(obj, Is.InstanceOf(typeof(MetricWireModel)));

            var wireModel = obj as MetricWireModel;
            Assert.That(wireModel, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(wireModel.MetricNameModel.Name, Is.EqualTo(name));
                Assert.That(wireModel.DataModel.Value0, Is.EqualTo(2));
                Assert.That(wireModel.DataModel.Value1, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value2, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value3, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value4, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value5, Is.EqualTo(0));
            });
        }


        [Test]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDistributed_TryBuildAcceptPayloadIgnoredUntrustedAccount()
        {
            var name = "Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount";

            var wireModel = _metricBuilder.TryBuildAcceptPayloadIgnoredUntrustedAccount();
            Assert.That(wireModel, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(wireModel.MetricNameModel.Name, Is.EqualTo(name));
                Assert.That(wireModel.DataModel.Value0, Is.EqualTo(1));
                Assert.That(wireModel.DataModel.Value1, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value2, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value3, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value4, Is.EqualTo(0));
                Assert.That(wireModel.DataModel.Value5, Is.EqualTo(0));
            });
        }

        #endregion DistributedTracing

        #region DataUsageMetrics

        [Test]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDataUsage_TryBuildSupportabilityDataUsageMetric()
        {
            var name = "Supportability/DotNET/Collector/Output/Bytes";

            var wireModel = _metricBuilder.TryBuildSupportabilityDataUsageMetric(name, 1, 2, 3);
            Assert.That(wireModel, Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(wireModel.MetricNameModel.Name, Is.EqualTo(name));
                Assert.That(wireModel.DataModel.Value0, Is.EqualTo(1));
                Assert.That(wireModel.DataModel.Value1, Is.EqualTo(2));
                Assert.That(wireModel.DataModel.Value2, Is.EqualTo(3));
            });
        }

        #endregion DataUsageMetrics
    }
}
