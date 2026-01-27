// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

public class TrackedWrapper
{
    public readonly IWrapper Wrapper;
    private int _numberOfConsecutiveFailures;
    public int NumberOfConsecutiveFailures => _numberOfConsecutiveFailures;

    public TrackedWrapper(IWrapper wrapper)
    {
        Wrapper = wrapper;
    }

    public void NoticeSuccess()
    {
        _numberOfConsecutiveFailures = 0;
    }

    public void NoticeFailure()
    {
        Interlocked.Increment(ref _numberOfConsecutiveFailures);
    }
}