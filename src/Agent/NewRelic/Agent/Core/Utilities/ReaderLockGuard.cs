// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

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