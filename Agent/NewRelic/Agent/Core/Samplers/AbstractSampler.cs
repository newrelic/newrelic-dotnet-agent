using System;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Samplers
{
	public abstract class AbstractSampler : ConfigurationBasedService
	{
		private readonly IScheduler _scheduler;

		private readonly TimeSpan _frequency;

		protected virtual bool Enabled
		{
			get
			{
				return !_configuration.DisableSamplers;
			}
		}

		protected AbstractSampler(IScheduler scheduler, TimeSpan frequency)
		{
			_scheduler = scheduler;
			_frequency = frequency;
		}

		public abstract void Sample();

		public override void Dispose()
		{
			base.Dispose();
			Stop();
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			Stop();
			Start();
		}

		public virtual void Start()
		{
			if (!Enabled)
			{
				return;
			}

			_scheduler.ExecuteEvery(Sample, _frequency);
		}

		protected virtual void Stop()
		{
			Log.Finest($"Sampler {this.GetType().FullName} has been requested to stop.");
			_scheduler.StopExecuting(Sample);
		}
	}
}
