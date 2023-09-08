// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.Core.Metrics
{
    [TestFixture]
    public class CATSupportabilityMetricCounterTests
    {
        private CATSupportabilityMetricCounters _metricCounters;
        private List<MetricWireModel> _metrics;

        [SetUp]
        public void Setup()
        {
            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _metricCounters = new CATSupportabilityMetricCounters(metricBuilder);

            _metrics = new List<MetricWireModel>();
            _metricCounters.RegisterPublishMetricHandler(metric => _metrics.Add(metric));
        }

        [Test]
        public void AllMetricCountsAreZero_WhenNoMethodsRecorded()
        {
            _metricCounters.CollectMetrics();
            CollectionAssert.IsEmpty(_metrics);
        }

        [Test]
        public void MetricValuesAreCorrect()
        {
            var assertions = new List<Action>();
            var enumVals = Enum.GetValues(typeof(CATSupportabilityCondition)).Cast<CATSupportabilityCondition>().ToList();

            //Call each supportability metric a different number of times so ensure that they aggrgate
            assertions.Add(() => Assert.AreEqual(enumVals.Count, _metrics.Count, $"Expected {enumVals.Count} metrics, Actual {_metrics.Count}"));
            foreach (var enumVal in enumVals)
            {
                var countHits = (int)enumVal + 10;
                for (var i = 0; i < countHits; i++)
                {
                    _metricCounters.Record(enumVal);
                }

                var expectedName = MetricNames.GetSupportabilityCATConditionMetricName(enumVal);

                //Ensure that we can find out metric
                assertions.Add(() => Assert.IsNotNull(_metrics.FirstOrDefault(x => x.MetricName.Name == expectedName),
                    $"Unable to find metric '{expectedName}'"));

                //Ensure its count matches the number of times the supportability metric was called
                assertions.Add(() => Assert.AreEqual(countHits, _metrics.FirstOrDefault(x => x.MetricName.Name == MetricNames.GetSupportabilityCATConditionMetricName(enumVal)).Data.Value0));
            }

            //Act
            _metricCounters.CollectMetrics();

            //Assert
            NrAssert.Multiple(assertions.ToArray());
        }
    }
}
