// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

/// <summary>
/// The response the DACI's main body says is sufficient on success: an empty dictionary, identical to
/// StartThreadProfilerCommand/StopThreadProfilerCommand's own success ack. When <see cref="ContinuousProfilingCommandResult.Exceptions"/>
/// is non-empty (an unsupported/unrecognized profile-type token, or the requested profile type actually
/// failed to start), joins them into the "errors" key -- the convention StartThreadProfilerCommand/
/// InstrumentationUpdateCommand already use for a command that partially or fully failed -- rather than
/// silently discarding them (see H5).
/// </summary>
public class AckOnlyContinuousProfilerResponseFormatter : IContinuousProfilerCommandResponseFormatter
{
    public IDictionary<string, object> Format(ContinuousProfilingCommandResult result)
    {
        if (result.Exceptions.Count == 0)
            return new Dictionary<string, object>();

        var message = string.Join("; ", result.Exceptions.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        return new Dictionary<string, object> { { "errors", message } };
    }
}
