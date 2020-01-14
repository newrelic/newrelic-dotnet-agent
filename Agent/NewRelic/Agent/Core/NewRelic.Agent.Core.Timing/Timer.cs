using System;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Timing
{
	class Timer : ITimer
	{
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
