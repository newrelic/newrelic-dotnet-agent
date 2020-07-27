using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.Utilities
{
    class WriterLockGuard : IDisposable
    {
        [NotNull]
        private readonly ReaderWriterLock _lock;

        public WriterLockGuard([NotNull] ReaderWriterLock readerWriterLock)
        {
            _lock = readerWriterLock;
        }

        public WriterLockGuard Acquire()
        {
            _lock.AcquireWriterLock(Timeout.Infinite);
            return this;
        }

        public void Dispose()
        {
            _lock.ReleaseWriterLock();
        }
    }
}
