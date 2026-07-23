// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Exposes whether a continuous-profiling session is currently active. Consumed by the
/// thread-profiling service so the two profilers do not run concurrently.
/// </summary>
public interface IContinuousProfilingSessionControl
{
    bool IsActive { get; }
}
