// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

class WriterLockGuard : IDisposable
{
    private readonly ReaderWriterLock _lock;

    public WriterLockGuard(ReaderWriterLock readerWriterLock)
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