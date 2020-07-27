using System;
using System.Threading;

namespace NewRelic.Dispatchers.Utilities
{
    public class ReaderLockGuard : IDisposable
    {
        private readonly ReaderWriterLock _lock;

        public ReaderLockGuard(ReaderWriterLock readerWriterLock)
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
