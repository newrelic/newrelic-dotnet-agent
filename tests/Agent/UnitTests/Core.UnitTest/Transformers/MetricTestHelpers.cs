/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
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
                () => Assert.AreEqual(1, generatedMetrics[metricName].Value0),
                () => Assert.AreEqual(expectedValue, generatedMetrics[metricName].Value1),
                () => Assert.AreEqual(expectedValue, generatedMetrics[metricName].Value2),
                () => Assert.AreEqual(expectedValue, generatedMetrics[metricName].Value3),
                () => Assert.AreEqual(expectedValue, generatedMetrics[metricName].Value4),
                () => Assert.AreEqual(expectedValue * expectedValue, generatedMetrics[metricName].Value5)
            );
        }
    }
}
