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
                () => Assert.That(generatedMetrics[metricName].Value0, Is.EqualTo(1)),
                () => Assert.That(generatedMetrics[metricName].Value1, Is.EqualTo(expectedValue)),
                () => Assert.That(generatedMetrics[metricName].Value2, Is.EqualTo(expectedValue)),
                () => Assert.That(generatedMetrics[metricName].Value3, Is.EqualTo(expectedValue)),
                () => Assert.That(generatedMetrics[metricName].Value4, Is.EqualTo(expectedValue)),
                () => Assert.That(generatedMetrics[metricName].Value5, Is.EqualTo(expectedValue * expectedValue))
            );
        }
    }
}
