/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Threading;

namespace NewRelic.SystemExtensions.Threading
{
    public static class ReaderWriterLockSlimExtensions
    {
        public static Func<IDisposable> ReusableDisposableReadLock(this ReaderWriterLockSlim readerWriterLockSlim)
        {
            if (readerWriterLockSlim == null)
                throw new ArgumentNullException("readerWriterLockSlim");

            return ReusableDisposableLock(readerWriterLockSlim.EnterReadLock, readerWriterLockSlim.ExitReadLock);
        }
        public static Func<IDisposable> ReusableDisposableWriteLock(this ReaderWriterLockSlim readerWriterLockSlim)
        {
            if (readerWriterLockSlim == null)
                throw new ArgumentNullException("readerWriterLockSlim");

            return ReusableDisposableLock(readerWriterLockSlim.EnterWriteLock, readerWriterLockSlim.ExitWriteLock);
        }
        private static Func<IDisposable> ReusableDisposableLock(
            Action acquire, Action release)
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
            private readonly Action _action;

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
