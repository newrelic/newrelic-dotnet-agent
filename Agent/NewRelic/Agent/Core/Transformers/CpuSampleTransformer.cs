using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Transformers
{
	public interface ICpuSampleTransformer
	{
		void Transform([NotNull] ImmutableCpuSample sample);
	}

	public class CpuSampleTransformer : ICpuSampleTransformer
	{
		[NotNull]
		protected readonly IMetricBuilder MetricBuilder;

		[NotNull]
		private readonly IMetricAggregator _metricAggregator;

		public CpuSampleTransformer([NotNull] IMetricBuilder metricBuilder, [NotNull] IMetricAggregator metricAggregator)
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
				Log.Debug("No CPU metrics will be reported: " + ex);
            }
		}

		private void RecordMetric([CanBeNull] MetricWireModel metric)
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

		private Single GetCpuUserUtilization(TimeSpan cpuUserTime, DateTime currentSampleTime, DateTime lastSampleTime, Int32 processorCount)
		{
			var wallClockTimeMs = (currentSampleTime - lastSampleTime).TotalMilliseconds;
			var cpuUserTimeMs = cpuUserTime.TotalMilliseconds;
			var cpuUserUtilizationMs = (Single) (cpuUserTimeMs / (wallClockTimeMs * processorCount));
			if (Single.IsNaN(cpuUserUtilizationMs) || Single.IsInfinity(cpuUserUtilizationMs))
				throw new Exception($"Invalid CPU Utilization. CPU time: {cpuUserTimeMs} (ms), Real time: {wallClockTimeMs} (ms)");

			return cpuUserUtilizationMs;
		}
	}
}