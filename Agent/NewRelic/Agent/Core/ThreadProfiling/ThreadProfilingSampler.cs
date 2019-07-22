using JetBrains.Annotations;
using NewRelic.Agent.Core.Time;
using NewRelic.Core.Logging;
using System;
using System.Diagnostics;
using System.Threading;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	/// <summary>
	/// Performs polling of the unmanaged thread profiler for samples of stack snapshots.
	/// </summary>
	public class ThreadProfilingSampler : IThreadProfilingSampler
	{
		#region Background Sampling Worker Thread

		/// <summary>
		/// Tracks the state of the background sampling worker.  1: worker has been scheduled/is running.  0: no worker has been scheduled.
		/// </summary>
		private Int32 _workerRunning = 0;

		/// <summary>
		/// Used to signal the background thread to terminate and stop sampling
		/// </summary>
		[NotNull]
		private ManualResetEventSlim _shutdownEvent = new ManualResetEventSlim(false);

		private Thread _samplingWorker = null;
		#endregion

		public ThreadProfilingSampler()
		{
		}

		public bool Start(UInt32 frequencyInMsec, UInt32 durationInMsec, [NotNull]ISampleSink sampleSink, [NotNull] INativeMethods nativeMethods)
		{
			_shutdownEvent.Reset();

			//atomic compare and set - if _workerRunning was a zero, it's now a 1 and create worker will be true
			bool createWorker = 0 == Interlocked.CompareExchange(ref _workerRunning, 1, 0);
			if (createWorker)
			{
				_samplingWorker = new Thread( () => InternalPolling_WaitCallback(frequencyInMsec, durationInMsec, sampleSink, nativeMethods) )
				{
					IsBackground = true
				};
				_samplingWorker.Start();
			}

			//return whether or not we created a session
			return createWorker; 
		}

		public void Stop()
		{
			//if we have already asked for termination or the background thread is not operational, we are done here.
			if (_shutdownEvent.Wait(0) || 1 == _workerRunning)
				return;

			//signal sampling worker to terminate
			_shutdownEvent.Set();

			//wait for the sampling worker to terminate
			if (_samplingWorker != null)
			{
				_samplingWorker.Join();
				_samplingWorker = null;
			}
		}

		#region Private Workers

		/// <summary>
		/// Polls for profiled threads.
		/// </summary>
		private void InternalPolling_WaitCallback(UInt32 frequencyInMsec, UInt32 durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods)
		{
			Stopwatch sw = Stopwatch.StartNew();

			//drift calculation support
			int intervalMilliseconds = (int)frequencyInMsec;
			//if the time used to profile and update tree was more than this percentage of the sampling freq, count it in samplesExceedingThreshold
			const double ReportDriftThreshold = 0.1;  
			int reportingThresholdMilliseconds = (int)Math.Truncate(intervalMilliseconds * ReportDriftThreshold);

			int samplesExceedingThreshold = 0;
			int samples = 0;

			var lastTickOfSamplingPeriod = DateTime.UtcNow.AddMilliseconds(durationInMsec).Ticks;
			try
			{
				// Accounting for drift
				int elapsedMilliseconds = 0;
				while (!_shutdownEvent.Wait(intervalMilliseconds - elapsedMilliseconds))
				{
					long startTick = sw.ElapsedTicks;

					if (DateTime.UtcNow.Ticks > lastTickOfSamplingPeriod)
					{
						_shutdownEvent.Set();
						Log.Debug("InternalPolling_WaitCallback: Duration Elapsed -- Stopping Sampler");
						break;
					}

					try
					{
						var threadSnapshots = nativeMethods.GetProfileWithRelease(out int result);
						if (result >= 0)
						{
							++samples;
							sampleSink.SampleAcquired(threadSnapshots);
						}
						else
						{
							Log.Error($"Thread Profile sampling failed. ({result:X})");
						}

					}
					catch (Exception ex)
					{
						Log.Error(ex);
					}

					//drift
					var ticksUsed = sw.ElapsedTicks - startTick;
					elapsedMilliseconds = Math.Min((int)(ticksUsed / TimeSpan.TicksPerMillisecond), intervalMilliseconds);
					if (elapsedMilliseconds >= reportingThresholdMilliseconds)
					{
						++samplesExceedingThreshold;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex);
			}
			finally
			{
				Log.Info($"samples ({samples}) exceeding threshold: {samplesExceedingThreshold}");

				sampleSink.SamplingComplete();

				nativeMethods.ShutdownNativeThreadProfiler();
				_workerRunning = 0;
			}
		}
		#endregion
	}
}
