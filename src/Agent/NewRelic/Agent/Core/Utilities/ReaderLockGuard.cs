using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utilities
{
	public class ReaderLockGuard : IDisposable
	{
		[NotNull]
		private readonly ReaderWriterLock _lock;

		public ReaderLockGuard([NotNull] ReaderWriterLock readerWriterLock)
		{
			_lock = readerWriterLock;
		}

		public ReaderLockGuard Acquire()
		{
			_lock.AcquireReaderLock(Timeout.Infinite);
			return this;
		}

		public void Dispose()
		{
			_lock.ReleaseReaderLock();
		}
	}
}
