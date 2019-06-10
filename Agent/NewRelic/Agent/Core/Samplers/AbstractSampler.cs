using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Samplers
{
	public abstract class AbstractSampler : ConfigurationBasedService
	{
		[NotNull]
		private readonly IScheduler _scheduler;

		private readonly TimeSpan _frequency;

		protected virtual bool Enabled
		{
			get
			{
				return !_configuration.DisableSamplers;
			}
		}

		protected AbstractSampler([NotNull] IScheduler scheduler, TimeSpan frequency)
		{
			_scheduler = scheduler;
			_frequency = frequency;

			Start();
		}

		public abstract void Sample();

		public override void Dispose()
		{
			base.Dispose();
			_scheduler.StopExecuting(Sample);
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			_scheduler.StopExecuting(Sample);
			Start();
		}

		private void Start()
		{
			if (!Enabled)
				return;

			_scheduler.ExecuteEvery(Sample, _frequency);
		}

		protected void Stop()
		{
			_scheduler.StopExecuting(Sample);
		}
	}
}
