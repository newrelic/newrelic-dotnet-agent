// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System;

namespace NewRelic.Agent.Core.Metrics
{
    [TestFixture]
    public class ApiSupportabilityMetricCountersTests
    {
        private ApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;

        private List<MetricWireModel> _publishedMetrics;

        private static readonly List<ApiMethod> ApiMethods = Enum.GetValues(typeof(ApiMethod)).Cast<ApiMethod>().ToList();

        private const string SupportabilityPrefix = "Supportability/ApiInvocation/";

        [SetUp]
        public void SetUp()
        {
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();

            _apiSupportabilityMetricCounters = new ApiSupportabilityMetricCounters(metricBuilder);

            _publishedMetrics = new List<MetricWireModel>();
            _apiSupportabilityMetricCounters.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [Test]
        public void AllMetricCountsAreZero_WhenNoMethodsRecorded()
        {
            _apiSupportabilityMetricCounters.CollectMetrics();
            Assert.That(_publishedMetrics, Is.Empty);
        }

        [TestCaseSource(nameof(ApiMethods))]
        public void SingleMetricIsGenerated_WhenOneMethodIsRecorded(ApiMethod apiMethod)
        {
            _apiSupportabilityMetricCounters.Record(apiMethod);
            _apiSupportabilityMetricCounters.CollectMetrics();
            var metric = _publishedMetrics.Single();
            Assert.Multiple(() =>
            {
                Assert.That(metric.MetricNameModel.Name, Is.EqualTo(SupportabilityPrefix + apiMethod));
                Assert.That(metric.DataModel.Value0, Is.EqualTo(1));
            });
        }

        [Test]
        public void AllMetricsGenerated_WhenAllMethodsRecorded()
        {
            foreach (var apiMethod in ApiMethods)
            {
                _apiSupportabilityMetricCounters.Record(apiMethod);
            }

            _apiSupportabilityMetricCounters.CollectMetrics();
            var actualMetricNames = _publishedMetrics.Select(metric => metric.MetricNameModel.Name).ToList();
            var expectedMetricNames = ApiMethods.Select(x => SupportabilityPrefix + x.ToString()).ToList();

            Assert.That(actualMetricNames, Is.EquivalentTo(expectedMetricNames));
        }

        [TestCase(ApiMethod.InsertDistributedTraceHeaders, 5)]
        [TestCase(ApiMethod.AcceptDistributedTraceHeaders, 42)]
        public void CorrectMetricCounts_WhenMethodIsRecordedMultipleTimes(ApiMethod apiMethod, int recordCount)
        {
            for (var x = 0; x < recordCount; x++)
            {
                _apiSupportabilityMetricCounters.Record(apiMethod);
            }
            _apiSupportabilityMetricCounters.CollectMetrics();
            var metric = _publishedMetrics.Single();
            Assert.That(metric.DataModel.Value0, Is.EqualTo(recordCount));
        }
    }
}
