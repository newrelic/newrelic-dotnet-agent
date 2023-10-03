// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core.Logging;


namespace NewRelic.Agent.Core.Transformers
{
    public interface ICpuSampleTransformer
    {
        void Transform(ImmutableCpuSample sample);
    }

    public class CpuSampleTransformer : ICpuSampleTransformer
    {
        protected readonly IMetricBuilder MetricBuilder;

        private readonly IMetricAggregator _metricAggregator;

        public CpuSampleTransformer(IMetricBuilder metricBuilder, IMetricAggregator metricAggregator)
        {
            MetricBuilder = metricBuilder;
            _metricAggregator = metricAggregator;
        }

        public void Transform(ImmutableCpuSample sample)
        {
            try
            {
                var cpuUserTime = GetCpuUserTime(sample.CurrentUserProcessorTime, sample.LastUserProcessorTime);
                var cpuUserUtilization = GetCpuUserUtilization(cpuUserTime, sample.CurrentSampleTime, sample.LastSampleTime, sample.ProcessorCount);

                var unscopedCpuUserTimeMetric = MetricBuilder.TryBuildCpuUserTimeMetric(cpuUserTime);
                RecordMetric(unscopedCpuUserTimeMetric);

                var unscopedCpuUserUtilizationMetric = MetricBuilder.TryBuildCpuUserUtilizationMetric(cpuUserUtilization);
                RecordMetric(unscopedCpuUserUtilizationMetric);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "No CPU metrics will be reported: ");
            }
        }

        private void RecordMetric(MetricWireModel metric)
        {
            if (metric == null)
                return;

            _metricAggregator.Collect(metric);
        }

        private TimeSpan GetCpuUserTime(TimeSpan currentUserProcessorTime, TimeSpan lastUserProcessorTime)
        {
            var cpuUserTime = currentUserProcessorTime - lastUserProcessorTime;
            if (cpuUserTime < TimeSpan.Zero)
                throw new Exception($"Invalid CPU User Time. Current CPU time: {currentUserProcessorTime.TotalMilliseconds} (ms), and last CPU time: {lastUserProcessorTime.TotalMilliseconds} (ms), resulting in a negative CPU time");

            return cpuUserTime;
        }

        private float GetCpuUserUtilization(TimeSpan cpuUserTime, DateTime currentSampleTime, DateTime lastSampleTime, int processorCount)
        {
            var wallClockTimeMs = (currentSampleTime - lastSampleTime).TotalMilliseconds;
            var cpuUserTimeMs = cpuUserTime.TotalMilliseconds;
            var cpuUserUtilizationMs = (float)(cpuUserTimeMs / (wallClockTimeMs * processorCount));
            if (float.IsNaN(cpuUserUtilizationMs) || float.IsInfinity(cpuUserUtilizationMs))
                throw new Exception($"Invalid CPU Utilization. CPU time: {cpuUserTimeMs} (ms), Real time: {wallClockTimeMs} (ms)");

            return cpuUserUtilizationMs;
        }
    }
}
