// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Transformers
{
    public static class MetricTestHelpers
    {
        public static void CompareMetric(Dictionary<string, MetricDataWireModel> generatedMetrics, string metricName, float expectedValue)
        {
            NrAssert.Multiple(
                () => Assert.That(generatedMetrics[metricName].Value0, Is.EqualTo(1), message: $"{metricName}.Value0"),
                () => Assert.That(generatedMetrics[metricName].Value1, Is.EqualTo(expectedValue), message: $"{metricName}.Value1"),
                () => Assert.That(generatedMetrics[metricName].Value2, Is.EqualTo(expectedValue), message: $"{metricName}.Value2"),
                () => Assert.That(generatedMetrics[metricName].Value3, Is.EqualTo(expectedValue), message: $"{metricName}.Value3"),
                () => Assert.That(generatedMetrics[metricName].Value4, Is.EqualTo(expectedValue), message: $"{metricName}.Value4"),
                () => Assert.That(generatedMetrics[metricName].Value5, Is.EqualTo(expectedValue * expectedValue), message: $"{metricName}.Value5")
            );
        }

        public static void CompareCountMetric(Dictionary<string, MetricDataWireModel> generatedMetrics, string metricName, float expectedValue)
        {
            Assert.That(generatedMetrics[metricName].Value0, Is.EqualTo(expectedValue), message: $"{metricName}.Value0");
        }
    }
}
