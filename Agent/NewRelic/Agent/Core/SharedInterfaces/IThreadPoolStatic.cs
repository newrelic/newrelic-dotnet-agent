using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.SharedInterfaces
{
	public interface IThreadPoolStatic
	{
		Boolean QueueUserWorkItem([NotNull] WaitCallback callBack);
		Boolean QueueUserWorkItem([NotNull] WaitCallback callBack, Object state);

		void GetMaxThreads(out int countMaxWorkerThreads, out int countMaxCompletionThreads);
		void GetMinThreads(out int countMinWorkerThreads, out int countMinCompletionThreads);
		void GetAvailableThreads(out int countAvailWorkerThreads, out int countAvailCompletionThreads);
	}

	public class ThreadPoolStatic : IThreadPoolStatic
	{
		public void GetAvailableThreads(out int countAvailWorkerThreads, out int countAvailCompletionThreads)
		{
			ThreadPool.GetAvailableThreads(out countAvailWorkerThreads, out countAvailCompletionThreads);
		}

		public void GetMaxThreads(out int countMaxWorkerThreads, out int countMaxCompletionThreads)
		{
			ThreadPool.GetMaxThreads(out countMaxWorkerThreads, out countMaxCompletionThreads);
		}

		public void GetMinThreads(out int countMinWorkerThreads, out int countMinCompletionThreads)
		{
			ThreadPool.GetMinThreads(out countMinWorkerThreads, out countMinCompletionThreads);
		}

		public Boolean QueueUserWorkItem(WaitCallback callBack)
		{
			return ThreadPool.QueueUserWorkItem(callBack);
		}

		public Boolean QueueUserWorkItem(WaitCallback callBack, Object state)
		{
			return ThreadPool.QueueUserWorkItem(callBack, state);
		}
	}
}
