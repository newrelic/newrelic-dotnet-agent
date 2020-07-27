using System;
using System.Threading;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions.Threading
{
    public static class ReaderWriterLockSlimExtensions
    {

        [NotNull]
        public static Func<IDisposable> ReusableDisposableReadLock([NotNull] this ReaderWriterLockSlim readerWriterLockSlim)
        {
            if (readerWriterLockSlim == null)
                throw new ArgumentNullException("readerWriterLockSlim");

            return ReusableDisposableLock(readerWriterLockSlim.EnterReadLock, readerWriterLockSlim.ExitReadLock);
        }

        [NotNull]
        public static Func<IDisposable> ReusableDisposableWriteLock([NotNull] this ReaderWriterLockSlim readerWriterLockSlim)
        {
            if (readerWriterLockSlim == null)
                throw new ArgumentNullException("readerWriterLockSlim");

            return ReusableDisposableLock(readerWriterLockSlim.EnterWriteLock, readerWriterLockSlim.ExitWriteLock);
        }

        [NotNull]
        private static Func<IDisposable> ReusableDisposableLock(
            [NotNull] Action acquire, [NotNull] Action release)
        {

            var disposable = new DisposeDelegate(release);
            return () =>
            {
                acquire();
                return disposable;
            };
        }

        private sealed class DisposeDelegate : IDisposable
        {
            [NotNull] private readonly Action _action;

            public DisposeDelegate(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }

    }
}
