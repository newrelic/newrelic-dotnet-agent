// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
    public static class MetricTestHelpers
    {
        public static void CompareMetric(Dictionary<string, MetricDataWireModel> generatedMetrics, string metricName, float expectedValue)
        {
            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(1, generatedMetrics[metricName].Value0),
                () => ClassicAssert.AreEqual(expectedValue, generatedMetrics[metricName].Value1),
                () => ClassicAssert.AreEqual(expectedValue, generatedMetrics[metricName].Value2),
                () => ClassicAssert.AreEqual(expectedValue, generatedMetrics[metricName].Value3),
                () => ClassicAssert.AreEqual(expectedValue, generatedMetrics[metricName].Value4),
                () => ClassicAssert.AreEqual(expectedValue * expectedValue, generatedMetrics[metricName].Value5)
            );
        }
    }
}
