// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// Builds the start_continuous_profiler/stop_continuous_profiler agent command response payload from a
/// response-shape-agnostic <see cref="ContinuousProfilingCommandResult"/>.
/// </summary>
public interface IContinuousProfilerCommandResponseFormatter
{
    IDictionary<string, object> Format(ContinuousProfilingCommandResult result);
}
