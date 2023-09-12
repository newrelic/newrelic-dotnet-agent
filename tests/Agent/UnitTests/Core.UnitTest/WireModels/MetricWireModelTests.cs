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

        [Test]
        public void MetricWireModelStartsWithNameInString()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var wireModel = MetricWireModel.BuildMetric(_metricNameService, "originalName", null, metricData);

            var wireModelString = wireModel.ToString();

            Assert.AreEqual("originalName ()NewRelic.Agent.Core.WireModels.MetricDataWireModel", wireModelString);
        }

        [Test]
        public void ScopedMetricWireModelStartsWithNameInString()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var actual = MetricWireModel.BuildMetric(_metricNameService, "originalName", "theScope", metricData);

            // The code currently uses the type name of the data value and is not worth testing
            StringAssert.StartsWith("originalName (theScope)", actual.ToString());
        }

        [Test]
        public void MetricWireModelUsesReferenceEquals()
        {
            var metricData = MetricDataWireModel.BuildGaugeValue(1);
            var metricWireModel = MetricWireModel.BuildMetric(_metricNameService, "originalName", "theScope", metricData);

            Assert.IsTrue(metricWireModel.Equals(metricWireModel));
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

            Assert.IsFalse(metricWireModel.Equals(null));
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
            var data = metric1.Data;
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(3, data.Value1);
            Assert.AreEqual(2, data.Value2);

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
                if (current.MetricName.Scope == null)
                {
                    unscopedCount++;
                }
                else
                {
                    scopedCount++;
                    theScope = current.MetricName.Scope;
                    metricName = current.MetricName.Name;
                    scopedData = current.Data;
                }
            }

            Assert.AreEqual(1, scopedCount);
            Assert.AreEqual(0, unscopedCount);
            Assert.AreEqual("scope", theScope);
            Assert.AreEqual("DotNet/name", metricName);
            Assert.IsNotNull(scopedData);
            Assert.AreEqual(1, scopedData.Value0);
            Assert.AreEqual(3, scopedData.Value1);
            Assert.AreEqual(2, scopedData.Value2);
        }

        [TestCase("")]
        [TestCase(null)]
        public void AddMetricsToEngine_OneUnscopedMetricMissingScope(string empty)
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", empty,
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
            Assert.That(metric1, Is.Not.Null);

            var data = metric1.Data;
            Assert.NotNull(data);
            Assert.AreEqual(1, data.Value0);
            Assert.AreEqual(3, data.Value1);
            Assert.AreEqual(2, data.Value2);

            var collection = new MetricStatsCollection();
            metric1.AddMetricsToCollection(collection);

            var stats = collection.ConvertToJsonForSending(_metricNameService);

            foreach (var current in stats)
            {
                Assert.AreEqual("DotNet/name", current.MetricName.Name);
                Assert.AreEqual(null, current.MetricName.Scope);
                var myData = current.Data;
                Assert.AreEqual(1, myData.Value0);
                Assert.AreEqual(3, myData.Value1);
                Assert.AreEqual(2, myData.Value2);
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
                () => Assert.AreEqual("DotNet/name", mergedMetric.MetricName.Name),
                () => Assert.AreEqual(1, mergedMetric.Data.Value0),
                () => Assert.AreEqual(3, mergedMetric.Data.Value1),
                () => Assert.AreEqual(1, mergedMetric.Data.Value2),
                () => Assert.AreEqual(3, mergedMetric.Data.Value3),
                () => Assert.AreEqual(3, mergedMetric.Data.Value4),
                () => Assert.AreEqual(9, mergedMetric.Data.Value5)
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
                () => Assert.AreEqual("DotNet/name", mergedMetric.MetricName.Name),
                () => Assert.AreEqual(2, mergedMetric.Data.Value0),
                () => Assert.AreEqual(10, mergedMetric.Data.Value1),
                () => Assert.AreEqual(6, mergedMetric.Data.Value2),
                () => Assert.AreEqual(3, mergedMetric.Data.Value3),
                () => Assert.AreEqual(7, mergedMetric.Data.Value4),
                () => Assert.AreEqual(58, mergedMetric.Data.Value5)
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
                () => Assert.AreEqual("DotNet/name", mergedMetric.MetricName.Name),
                () => Assert.AreEqual(3, mergedMetric.Data.Value0),
                () => Assert.AreEqual(23, mergedMetric.Data.Value1),
                () => Assert.AreEqual(17, mergedMetric.Data.Value2),
                () => Assert.AreEqual(3, mergedMetric.Data.Value3),
                () => Assert.AreEqual(13, mergedMetric.Data.Value4),
                () => Assert.AreEqual(227, mergedMetric.Data.Value5)
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
                () => Assert.AreEqual("DotNet/name", mergedMetric.MetricName.Name),
                () => Assert.AreEqual(3, mergedMetric.Data.Value0),
                () => Assert.AreEqual(23, mergedMetric.Data.Value1),
                () => Assert.AreEqual(17, mergedMetric.Data.Value2),
                () => Assert.AreEqual(3, mergedMetric.Data.Value3),
                () => Assert.AreEqual(13, mergedMetric.Data.Value4),
                () => Assert.AreEqual(227, mergedMetric.Data.Value5)
            );
        }

        [Test]
        public void Merge_IgnoresNullMetrics()
        {
            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", "scope",
                MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));

            var mergedMetric = MetricWireModel.Merge(new[] { null, metric1, null });

            NrAssert.Multiple(
                () => Assert.AreEqual("DotNet/name", mergedMetric.MetricName.Name),
                () => Assert.AreEqual(1, mergedMetric.Data.Value0),
                () => Assert.AreEqual(3, mergedMetric.Data.Value1),
                () => Assert.AreEqual(1, mergedMetric.Data.Value2),
                () => Assert.AreEqual(3, mergedMetric.Data.Value3),
                () => Assert.AreEqual(3, mergedMetric.Data.Value4),
                () => Assert.AreEqual(9, mergedMetric.Data.Value5)
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

            Assert.AreEqual(expectedJson, serializedMetric);
        }

        [Test]
        public void MetricWireModel_SerializesCorrectlyUnscoped()
        {
            const string expectedJson = @"[{""name"":""DotNet/name""},[1,3.0,1.0,3.0,3.0,9.0]]";

            var metric1 = MetricWireModel.BuildMetric(_metricNameService, "DotNet/name", null, MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));

            var serializedMetric = JsonConvert.SerializeObject(metric1);

            Assert.AreEqual(expectedJson, serializedMetric);
        }

        #endregion

        #region Build Metrics

        [Test]
        public void BuildMetricWireModelUsesOriginalName()
        {
            var actual = MetricWireModel.BuildMetric(_metricNameService, "originalName", null, null);
            Assert.AreEqual("originalName", actual.MetricName.Name);
        }

        [Test]
        public void BuildMetricWireModelReturnsNullWhenNameIsNull()
        {
            var mockNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => mockNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(null);

            var actual = MetricWireModel.BuildMetric(mockNameService, "originalName", null, null);
            Assert.IsNull(actual);
        }

        [Test]
        public void BuildAggregateData()
        {
            var one = MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4));
            var two = MetricDataWireModel.BuildTimingData(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(2));
            var actual = MetricDataWireModel.BuildAggregateData(one, two);
            Assert.AreEqual(2, actual.Value0);
            Assert.AreEqual(12, actual.Value1);
            Assert.AreEqual(6, actual.Value2);
            Assert.AreEqual(5, actual.Value3);
            Assert.AreEqual(7, actual.Value4);
            Assert.AreEqual(one.Value5 + two.Value5, actual.Value5);
        }

        [Test]
        public void BuildGaugeMetric()
        {
            const int expectedValue = 21;

            var metricData = MetricDataWireModel.BuildGaugeValue(expectedValue);

            NrAssert.Multiple(
                () => Assert.AreEqual(1, metricData.Value0),
                () => Assert.AreEqual(expectedValue, metricData.Value1),
                () => Assert.AreEqual(expectedValue, metricData.Value2),
                () => Assert.AreEqual(expectedValue, metricData.Value3),
                () => Assert.AreEqual(expectedValue, metricData.Value4),
                () => Assert.AreEqual(expectedValue * expectedValue, metricData.Value5)
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
                () => Assert.AreEqual(count, metricData.Value0),
                () => Assert.AreEqual(value, metricData.Value1),
                () => Assert.AreEqual(value, metricData.Value2),
                () => Assert.AreEqual(min, metricData.Value3),
                () => Assert.AreEqual(max, metricData.Value4),
                () => Assert.AreEqual(sumSquares, metricData.Value5)
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
                () => Assert.AreEqual(callCount, metricData.Value0),
                () => Assert.AreEqual(dataSent, metricData.Value1),
                () => Assert.AreEqual(dataReceived, metricData.Value2)
            );
        }

        [TestCase(true, "CPU/WebTransaction")]
        [TestCase(false, "CPU/OtherTransaction")]
        public void BuildCpuTimeRollupMetric(bool isWebTransaction, string expectedMetricName)
        {
            var actual = _metricBuilder.TryBuildCpuTimeRollupMetric(isWebTransaction, TimeSpan.FromSeconds(2));

            NrAssert.Multiple(
                () => Assert.AreEqual(expectedMetricName, actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value1)
            );
        }

        [TestCase(Metrics.MetricNames.WebTransactionPrefix, "CPU/WebTransaction/transactionName")]
        [TestCase(Metrics.MetricNames.OtherTransactionPrefix, "CPU/OtherTransaction/transactionName")]
        public void BuildCpuTimeMetric(string transactionPrefix, string expectedMetricName)
        {
            var transactionMetricName = new TransactionMetricName(transactionPrefix, "transactionName");
            var actual = _metricBuilder.TryBuildCpuTimeMetric(transactionMetricName, TimeSpan.FromSeconds(2));

            NrAssert.Multiple(
                () => Assert.AreEqual(expectedMetricName, actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value1)
            );
        }

        [Test]
        public void BuildSupportabilityGaugeMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityGaugeMetric("metricName", 2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/metricName", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value1)
            );
        }

        [Test]
        public void BuildDotnetCoreVersionMetric()
        {
            var actual = _metricBuilder.TryBuildDotnetCoreVersionMetric(DotnetCoreVersion.Other);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Dotnet/NetCore/Other", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildAgentVersionByHostMetric()
        {
            var actual = _metricBuilder.TryBuildAgentVersionByHostMetric("hostName", "10.0.0.0");

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/AgentVersion/hostName/10.0.0.0", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildMetricHarvestAttemptMetric()
        {
            var actual = _metricBuilder.TryBuildMetricHarvestAttemptMetric();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/MetricHarvest/transmit", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildTransactionEventReservoirResizedMetric()
        {
            var actual = _metricBuilder.TryBuildTransactionEventReservoirResizedMetric();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/AnalyticsEvents/TryResizeReservoir", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildTransactionEventsRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildTransactionEventsRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/AnalyticsEvents/TotalEventsRecollected", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildCustomEventReservoirResizedMetric()
        {
            var actual = _metricBuilder.TryBuildCustomEventReservoirResizedMetric();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Events/Customer/TryResizeReservoir", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildCustomEventsRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildCustomEventsRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Events/Customer/TotalEventsRecollected", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildErrorTracesRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildErrorTracesRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Errors/TotalErrorsRecollected", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildSqlTracesRecollectedMetric()
        {
            var actual = _metricBuilder.TryBuildSqlTracesRecollectedMetric(2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/SqlTraces/TotalSqlTracesRecollected", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildFeatureEnabledMetric()
        {
            var actual = _metricBuilder.TryBuildFeatureEnabledMetric("featureName");

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/FeatureEnabled/featureName", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void BuildLinuxOsMetric(bool isLinux, float expectedCount)
        {
            var actual = _metricBuilder.TryBuildLinuxOsMetric(isLinux);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/OS/Linux", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(expectedCount, actual.Data.Value1)
            );
        }

        [Test]
        public void BuildBootIdErrorMetric()
        {
            var actual = _metricBuilder.TryBuildBootIdError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/boot_id/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildKubernetesUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildKubernetesUsabilityError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/kubernetes/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildAwsUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildAwsUsabilityError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/aws/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildAzureUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildAzureUsabilityError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/azure/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildPcfUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildPcfUsabilityError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/pcf/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildGcpUsabilityErrorMetric()
        {
            var actual = _metricBuilder.TryBuildGcpUsabilityError();

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/utilization/gcp/error", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildSupportabilityEndpointMethodErrorAttemptsMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityEndpointMethodErrorAttempts("endpoint");

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Agent/Collector/endpoint/Attempts", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(1, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildSupportabilityPayloadsDroppedDueToMaxPayloadLimitMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit("endpoint", 2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/DotNet/Collector/MaxPayloadSizeLimit/endpoint", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
            );
        }

        [Test]
        public void BuildSupportabilityLoggingEventsDroppedMetric()
        {
            var actual = _metricBuilder.TryBuildSupportabilityLoggingEventsDroppedMetric(2);

            NrAssert.Multiple(
                () => Assert.AreEqual("Supportability/Logging/Forwarding/Dropped", actual.MetricName.Name),
                () => Assert.IsNull(actual.MetricName.Scope),
                () => Assert.AreEqual(2, actual.Data.Value0)
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

            Assert.That(wireModel.MetricName.Name, Is.EqualTo(name));
            Assert.That(wireModel.Data.Value0, Is.EqualTo(1));
            Assert.That(wireModel.Data.Value1, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value2, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value3, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value4, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value5, Is.EqualTo(0));
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

            Assert.That(wireModel.MetricName.Name, Is.EqualTo(name));
            Assert.That(wireModel.Data.Value0, Is.EqualTo(2));
            Assert.That(wireModel.Data.Value1, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value2, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value3, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value4, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value5, Is.EqualTo(0));
        }


        [Test]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDistributed_TryBuildAcceptPayloadIgnoredUntrustedAccount()
        {
            var name = "Supportability/DistributedTrace/AcceptPayload/Ignored/UntrustedAccount";

            var wireModel = _metricBuilder.TryBuildAcceptPayloadIgnoredUntrustedAccount();
            Assert.That(wireModel, Is.Not.Null);

            Assert.That(wireModel.MetricName.Name, Is.EqualTo(name));
            Assert.That(wireModel.Data.Value0, Is.EqualTo(1));
            Assert.That(wireModel.Data.Value1, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value2, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value3, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value4, Is.EqualTo(0));
            Assert.That(wireModel.Data.Value5, Is.EqualTo(0));
        }

        #endregion DistributedTracing

        #region DataUsageMetrics

        [Test]
        public void MetricWireModelTests_MetricBuilder_SupportabilityDataUsage_TryBuildSupportabilityDataUsageMetric()
        {
            var name = "Supportability/DotNET/Collector/Output/Bytes";

            var wireModel = _metricBuilder.TryBuildSupportabilityDataUsageMetric(name, 1, 2, 3);
            Assert.That(wireModel, Is.Not.Null);

            Assert.That(wireModel.MetricName.Name, Is.EqualTo(name));
            Assert.That(wireModel.Data.Value0, Is.EqualTo(1));
            Assert.That(wireModel.Data.Value1, Is.EqualTo(2));
            Assert.That(wireModel.Data.Value2, Is.EqualTo(3));
        }

        #endregion DataUsageMetrics
    }
}
