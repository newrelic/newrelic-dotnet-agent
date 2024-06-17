// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Transformers
{
    public interface IGcSampleTransformer
    {
        void Transform(Dictionary<GCSampleType, float> sampleValues);
    }

    public class GcSampleTransformer : IGcSampleTransformer
    {
        private readonly IMetricBuilder _metricBuilder;

        private readonly IMetricAggregator _metricAggregator;

        public GcSampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator)
        {
            _metricBuilder = metricBuilder;
            _metricAggregator = metricAggregator;

            _metricBuilderHandlers = new Dictionary<GCSampleType, Func<GCSampleType, float, MetricWireModel>>()
            {
                { GCSampleType.HandlesCount, CreateMetric_Gauge},
                { GCSampleType.InducedCount, CreateMetric_Count},
                { GCSampleType.PercentTimeInGc, CreateMetric_Percent},

                { GCSampleType.Gen0Size, CreateMetric_ByteData},
                { GCSampleType.Gen0Promoted, CreateMetric_ByteData},
                { GCSampleType.Gen0CollectionCount, CreateMetric_Count},

                { GCSampleType.Gen1Size, CreateMetric_ByteData},
                { GCSampleType.Gen1Promoted, CreateMetric_ByteData},
                { GCSampleType.Gen1CollectionCount, CreateMetric_Count},

                { GCSampleType.Gen2Size, CreateMetric_ByteData},
                { GCSampleType.Gen2Survived, CreateMetric_ByteData},
                { GCSampleType.Gen2CollectionCount, CreateMetric_Count},

                { GCSampleType.LOHSize, CreateMetric_ByteData},
                { GCSampleType.LOHSurvived, CreateMetric_ByteData},
            };
        }

        private readonly Dictionary<GCSampleType, Func<GCSampleType, float, MetricWireModel>> _metricBuilderHandlers;

        public void Transform(Dictionary<GCSampleType, float> sampleValues)
        {
            var metrics = new List<MetricWireModel>();

            foreach (var sampleValue in sampleValues)
            {
                var metricWireModel = _metricBuilderHandlers[sampleValue.Key](sampleValue.Key, sampleValue.Value);

                if (metricWireModel != null)
                {
                    metrics.Add(metricWireModel);
                }
            }

            RecordMetrics(metrics);
        }

        private MetricWireModel CreateMetric_Gauge(GCSampleType sampleType, float sampleValue)
        {
            return _metricBuilder.TryBuildGCGaugeMetric(sampleType, sampleValue);
        }

        private MetricWireModel CreateMetric_Count(GCSampleType sampleType, float sampleValue)
        {
            if (sampleValue < 0)
            {
                Log.Finest($"The GC Sampler encountered a negative value: {sampleValue}, for sample: {Enum.GetName(typeof(GCSampleType), sampleType)}");
                sampleValue = 0;
            }
            return _metricBuilder.TryBuildGCCountMetric(sampleType, (int)sampleValue);
        }

        private MetricWireModel CreateMetric_ByteData(GCSampleType sampleType, float sampleValue)
        {
            return _metricBuilder.TryBuildGCBytesMetric(sampleType, (long)sampleValue);
        }

        private MetricWireModel CreateMetric_Percent(GCSampleType sampleType, float sampleValue)
        {
            return _metricBuilder.TryBuildGCPercentMetric(sampleType, sampleValue);
        }

        private void RecordMetrics(IEnumerable<MetricWireModel> metrics)
        {
            foreach (var metric in metrics)
            {
                _metricAggregator.Collect(metric);
            }
        }
    }
}
