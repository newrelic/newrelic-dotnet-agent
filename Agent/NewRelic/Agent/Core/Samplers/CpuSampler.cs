using System;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
	public class CpuSampler : AbstractSampler
	{
		private readonly IAgentHealthReporter _agentHealthReporter;

		private readonly ICpuSampleTransformer _cpuSampleTransformer;

		private readonly int _processorCount;
		private DateTime _lastSampleTime;
		private TimeSpan _lastProcessorTime;
		private readonly IProcessStatic _processStatic;

		public CpuSampler(IScheduler scheduler, ICpuSampleTransformer cpuSampleTransformer, IAgentHealthReporter agentHealthReporter, IProcessStatic processStatic)
			: base(scheduler, TimeSpan.FromMinutes(1))
		{
			_agentHealthReporter = agentHealthReporter;
			_cpuSampleTransformer = cpuSampleTransformer;
			_processStatic = processStatic;

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

		private TimeSpan GetCurrentUserProcessorTime()
		{
			var process = _processStatic.GetCurrentProcess();
			return process.UserProcessorTime;
		}
	}

	public class ImmutableCpuSample
	{
		public readonly int ProcessorCount;

		public readonly DateTime LastSampleTime;

		public readonly TimeSpan LastUserProcessorTime;

		public readonly DateTime CurrentSampleTime;

		public readonly TimeSpan CurrentUserProcessorTime;

		public ImmutableCpuSample(int processorCount, DateTime lastSampleTime, TimeSpan lastUserProcessorTime, DateTime currentSampleTime, TimeSpan currentUserProcessorTime)
		{
			ProcessorCount = processorCount;
			LastSampleTime = lastSampleTime;
			LastUserProcessorTime = lastUserProcessorTime;
			CurrentSampleTime = currentSampleTime;
			CurrentUserProcessorTime = currentUserProcessorTime;
		}
	}
}
