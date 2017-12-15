using System;
using System.Diagnostics;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;

namespace NewRelic.Agent.Core.Samplers
{
	public class CpuSampler : AbstractSampler
	{
		[NotNull]
		private readonly IAgentHealthReporter _agentHealthReporter;

		[NotNull]
		private readonly ICpuSampleTransformer _cpuSampleTransformer;

		private readonly Int32 _processorCount;
		private DateTime _lastSampleTime;
		private TimeSpan _lastProcessorTime;

		public CpuSampler([NotNull] IScheduler scheduler, [NotNull] ICpuSampleTransformer cpuSampleTransformer, [NotNull] IAgentHealthReporter agentHealthReporter)
			: base(scheduler, TimeSpan.FromMinutes(1))
		{
			_agentHealthReporter = agentHealthReporter;
			_cpuSampleTransformer = cpuSampleTransformer;

			try
			{
				_processorCount = System.Environment.ProcessorCount;
				_lastSampleTime = DateTime.UtcNow;
				_lastProcessorTime = GetCurrentUserProcessorTime();
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to get CPU sample.  No CPU metrics will be reported.  Error : {ex}");
				Stop();
			}
		}

		public override void Sample()
		{
			try
			{
				var currentSampleTime = DateTime.UtcNow;
				var currentProcessorTime = GetCurrentUserProcessorTime();
				var immutableCpuSample = new ImmutableCpuSample(_processorCount, _lastSampleTime, _lastProcessorTime, currentSampleTime, currentProcessorTime);
				_cpuSampleTransformer.Transform(immutableCpuSample);
				_lastSampleTime = currentSampleTime;
				_lastProcessorTime = currentProcessorTime;
			}
			catch (Exception ex)
			{
				Log.Error($"Unable to get CPU sample.  No CPU metrics will be reported.  Error : {ex}");
				Stop();
			}
		}

		private static TimeSpan GetCurrentUserProcessorTime()
		{
			using (var process = Process.GetCurrentProcess())
			{
				return process.UserProcessorTime;
			}
		}
	}

	public class ImmutableCpuSample
	{
		[NotNull]
		public readonly Int32 ProcessorCount;

		[NotNull]
		public readonly DateTime LastSampleTime;

		[NotNull]
		public readonly TimeSpan LastUserProcessorTime;

		[NotNull]
		public readonly DateTime CurrentSampleTime;

		[NotNull]
		public readonly TimeSpan CurrentUserProcessorTime;

		public ImmutableCpuSample(Int32 processorCount, DateTime lastSampleTime, TimeSpan lastUserProcessorTime, DateTime currentSampleTime, TimeSpan currentUserProcessorTime)
		{
			ProcessorCount = processorCount;
			LastSampleTime = lastSampleTime;
			LastUserProcessorTime = lastUserProcessorTime;
			CurrentSampleTime = currentSampleTime;
            CurrentUserProcessorTime = currentUserProcessorTime;
		}
	}
}
