// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// The response the DACI's main body says is sufficient: an empty dictionary, identical to
/// StartThreadProfilerCommand/StopThreadProfilerCommand's own success ack. The command succeeded or failed
/// via CommandService's normal error-dictionary path (see AbstractCommand-derived classes); there is no
/// additional payload.
/// </summary>
public class AckOnlyContinuousProfilerResponseFormatter : IContinuousProfilerCommandResponseFormatter
{
    public IDictionary<string, object> Format(ContinuousProfilingCommandResult result) => new Dictionary<string, object>();
}
