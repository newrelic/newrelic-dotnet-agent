﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	#region Delegates for PInvoke calls to unmanaged thread profiler
	
	/// <summary>
	/// Delegate for a callback with an unmanaged stack snapshot payload.
	/// </summary>
	/// <param name="pArrayOfInt">Array of function identifiers for <paramref name="threadId"/>.</param>
	/// <param name="threadId">Thread Id for this stack snapshot.</param>
	/// <param name="length">Number of function identifies in <paramref name="pArrayOfInt"/>.</param>
	public delegate void StackSnapshotSuccessCallback(IntPtr pArrayOfInt, UIntPtr threadId, Int32 length);
	public delegate void StackSnapshotFailedCallback(UIntPtr threadId, UInt32 errorCode);
	public delegate void StackSnapshotCompleteCallback();
	#endregion

	/// <summary>
	/// Performs polling of the unmanaged thread profiler for samples of stack snapshots.
	/// </summary>
	public class ThreadProfilingSampler : IThreadProfilingSampler
	{
		#region PInvoke Targets and Delegate Instances

		static IntPtr _successProfileDelegateFunction = IntPtr.Zero;
		static IntPtr _failureProfileDelegateFunction = IntPtr.Zero;
		static IntPtr _completeDelegateFunction = IntPtr.Zero;

		StackSnapshotSuccessCallback _callbackDelegateProfiledThread;
		StackSnapshotFailedCallback _failedProfileCallbackDelegate;
		StackSnapshotCompleteCallback _completeCallbackDelegate;
		#endregion

		[NotNull]
		private readonly IAgent _agent;
		[NotNull]
		private IScheduler _scheduler;
		private readonly INativeMethods _nativeMethods;

		#region Polling Variables
		
		// This will enable the thread to pause and
		// also enable the thread to terminate
		[NotNull]
		private ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

		// Maintains if the polling thread is already active
		private Boolean _isPollingActivated;

		// Thread Synchronisation instance
		[NotNull]
		private readonly Object _syncObj = new Object();
		[NotNull]
		private readonly Object _syncProfiledThread = new Object();

		public Int32 NumberSamplesInSession { get; set; }

		#endregion

		#region Duration Handling

		private DateTime _startOfDuration;

		public bool SamplingInSession { get { return _isPollingActivated && !DurationElapsed(); } }

		#endregion

		// i.e.,  this is a dictionary of ManagedThreadId, Total Call Count
		[NotNull]
		public readonly Dictionary<UIntPtr, Int32> ManagedThreadsFromProfiler;

		private UInt32 _frequencyMsec;
		private UInt32 _durationMsec;

		public ThreadProfilingSampler([NotNull] IAgent agent, IScheduler scheduler, [NotNull] INativeMethods nativeMethods)
		{
			_agent = agent;
			_scheduler = scheduler;
			_nativeMethods = nativeMethods;

			ManagedThreadsFromProfiler = new Dictionary<UIntPtr, Int32>();

			InitializeUnmanagedConnection();
		}

		private void InitializeUnmanagedConnection()
		{
			try
			{
				_callbackDelegateProfiledThread  = ProfiledThreadDataCallback;
				_successProfileDelegateFunction = Marshal.GetFunctionPointerForDelegate(_callbackDelegateProfiledThread);

				_failedProfileCallbackDelegate = FailedProfileThreadDataCallback;
				_failureProfileDelegateFunction = Marshal.GetFunctionPointerForDelegate(_failedProfileCallbackDelegate);

				_completeCallbackDelegate = CompleteCallback;
				_completeDelegateFunction = Marshal.GetFunctionPointerForDelegate(_completeCallbackDelegate);
			}
			catch (Exception e)
			{
				Log.ErrorFormat("Exception inintializing unmanaged connection from Thread Profiling Sampler: {0}", e);
				throw;
			}
		}

		public bool Start(UInt32 frequencyInMsec, UInt32 durationInMsec)
		{
			_frequencyMsec = frequencyInMsec;
			_durationMsec = durationInMsec;
			NumberSamplesInSession = 0;

			_shutdownEvent.Close();
			_shutdownEvent = new ManualResetEvent(false);

			var startedNewSession = false;
			lock (_syncObj)
			{
				if (!_isPollingActivated)
				{
					_scheduler.ExecuteOnce(InternalPolling_WaitCallback, TimeSpan.Zero);
					_isPollingActivated = true;
					_startOfDuration = DateTime.UtcNow;
					startedNewSession = true;
				}
			}

			return startedNewSession;
		}

		public void Stop(Boolean reportData = true)
		{
			lock (_syncObj)
			{
				if (!_isPollingActivated)
					return;

				_shutdownEvent.Set();
				_isPollingActivated = false;

				if (reportData)
				{
					_agent.ThreadProfilingService.PerformAggregation();
				}
			}
		}

		public void FailedProfileThreadDataCallback(UIntPtr threadId, UInt32 errorCode)
		{
			try
			{
				if (errorCode == 1)
					_agent.ThreadProfilingService.AddLargeStackOverflowProfile(threadId);
				else
					_agent.ThreadProfilingService.AddFailedThreadProfile(threadId, errorCode);
			}
			catch (Exception e)
			{
				var msg = new StringBuilder(e.Message);
				if (e.InnerException != null)
					msg.Append(string.Format("FailedProfileThreadDataCallback EXCEPTION : {0}", e.InnerException.Message));

				Log.Debug(msg.ToString());
			}
		}

		public void CompleteCallback()
		{
		}

		public void ProfiledThreadDataCallback(IntPtr data, UIntPtr threadId, Int32 length)
		{
			lock (_syncProfiledThread)
			{
				try
				{
					if (length <= 0 || data == IntPtr.Zero)
						return;

					var stackInfo = new StackInfo();
					stackInfo.StoreFunctionIds(data, length);

					ManagedThreadsFromProfiler[threadId] = ManagedThreadsFromProfiler.GetValueOrDefault(threadId) + length;
					_agent.ThreadProfilingService.UpdateTree(stackInfo, 0);
				}
				catch (Exception e)
				{
					var msg = new StringBuilder(e.Message);
					if (e.InnerException != null)
						msg.Append(string.Format("ProfiledThreadDataCallback EXCEPTION : {0}", e.InnerException.Message));

					Log.Debug(msg.ToString());
				}
			}
		}

		#region Private Workers

		/// <summary>
		/// Polls for profiled threads.
		/// </summary>
		private void InternalPolling_WaitCallback()
		{
			while (!_shutdownEvent.WaitOne((Int32)_frequencyMsec, true))
			{
				if (DurationElapsed())
				{
					Log.Debug("InternalPolling_WaitCallback: Duration Elapsed -- Stopping Sampler");
					Stop();
				}

				try
				{
					_nativeMethods.RequestProfile(_successProfileDelegateFunction, _failureProfileDelegateFunction, _completeDelegateFunction);
				}
				catch (Exception ex)
				{
					Log.Error(ex);
				}
				NumberSamplesInSession++;
			}
		}

		private bool DurationElapsed()
		{
			return ((DateTime.UtcNow - _startOfDuration).TotalMilliseconds >= _durationMsec);
		}

#endregion
	}
}
