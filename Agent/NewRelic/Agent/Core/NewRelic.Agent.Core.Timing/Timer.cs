using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Timing
{
	class Timer : ITimer
	{
		[NotNull]
		private readonly Stopwatch _timer = Stopwatch.StartNew();

		public void Stop()
		{
			_timer.Stop();
		}

		public TimeSpan Duration => _timer.Elapsed;

		public bool IsRunning => _timer.IsRunning;

		void IDisposable.Dispose()
		{
			Stop();
		}
	}
}
