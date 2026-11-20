// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Stops method resolution from re-entering itself on one thread. Resolution runs reflection and logging, and an
/// instrumented method (for example customer XML on a BCL reflection or logging method) can be called from inside it;
/// that inner call would start another resolution, whose field is still empty, without end. The inner call gets null
/// instead (no tracer for it, nothing published), and the overload it belongs to resolves on its next call, so it is
/// never recorded as unresolved because of this. Never throws.
/// </summary>
public static class DistroResolutionGuard
{
    [ThreadStatic]
    private static bool _inProgress;

    /// <summary>
    /// Runs <paramref name="resolve"/> unless this thread is already inside one, and returns its result; returns null,
    /// without running it, on re-entry. The in-progress mark is always cleared afterwards, including when
    /// <paramref name="resolve"/> throws (the exception then propagates to the caller).
    /// </summary>
    public static T RunOnce<T>(Func<T> resolve) where T : class
    {
        if (_inProgress)
        {
            return null;
        }

        _inProgress = true;
        try
        {
            return resolve();
        }
        finally
        {
            _inProgress = false;
        }
    }
}
