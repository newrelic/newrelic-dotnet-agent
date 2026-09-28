// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

/// <summary>
/// Exposes whether a thread-profiling session is currently in-flight. Consumed by the
/// continuous-profiling service so the two profilers do not run concurrently: continuous
/// profiling defers its start while a thread-profiling session is active.
/// </summary>
public interface IThreadProfilingStatus
{
    bool IsThreadProfilingActive { get; }
}
