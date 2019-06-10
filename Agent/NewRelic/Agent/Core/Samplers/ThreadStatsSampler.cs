using System;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.SharedInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
	public class ThreadStatsSampler : AbstractSampler
	{
		private readonly IThreadPoolStatic _threadPoolProxy;
		private readonly IThreadStatsSampleTransformer _transformer;

		protected override bool Enabled => base.Enabled && _configuration.GenerateFullGcMemThreadMetricsEnabled;

		public ThreadStatsSampler(IScheduler scheduler, IThreadStatsSampleTransformer threadpoolStatsTransformer, IThreadPoolStatic threadpoolProxy) 
		 : base(scheduler, TimeSpan.FromSeconds(1))
		{
			_threadPoolProxy = threadpoolProxy;
			_transformer = threadpoolStatsTransformer;
		}

		public override void Sample()
		{
			try
			{
				_threadPoolProxy.GetMaxThreads(out int countWorkerThreadsMax, out int countCompletionThreadsMax);
				_threadPoolProxy.GetAvailableThreads(out int countWorkerThreadsAvail, out int countCompletionThreadsAvail);

				var stats = new ThreadStatsSample(countWorkerThreadsMax, countWorkerThreadsAvail, countCompletionThreadsMax, countCompletionThreadsAvail);
				
				_transformer.Transform(stats);
			}
			catch(Exception ex)
			{
				Log.Error($"Unable to get Threadpool stats sample.  No .Net Threadpool metrics will be reported.  Error : {ex}");
				Log.Error(ex);
				Stop();
			}
		}
	}

	public class ThreadStatsSample
	{
		public readonly int WorkerCountThreadsAvail;
		public readonly int WorkerCountThreadsUsed;
		public readonly int CompletionCountThreadsAvail;
		public readonly int CompletionCountThreadsUsed;

		public ThreadStatsSample(int countWorkerThreadsMax, int countWorkerThreadAvail, int countCompletionThreadssMax, int countCompletionThreadsAvail)
		{
			WorkerCountThreadsUsed = countWorkerThreadsMax - countWorkerThreadAvail;
			WorkerCountThreadsAvail = countWorkerThreadAvail;

			CompletionCountThreadsUsed = countCompletionThreadssMax - countCompletionThreadsAvail;
			CompletionCountThreadsAvail = countCompletionThreadsAvail;
		}
	}

	public enum ThreadType
	{
		Worker,
		Completion
	}

	public enum ThreadStatus
	{
		Available,
		InUse
	}

}
