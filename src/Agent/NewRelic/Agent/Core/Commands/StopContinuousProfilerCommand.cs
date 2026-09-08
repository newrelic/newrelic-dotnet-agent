// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

public class StopContinuousProfilerCommand : AbstractCommand
{
    private readonly IContinuousProfilingCommandTarget _target;
    private readonly IContinuousProfilerCommandResponseFormatter _responseFormatter;

    public StopContinuousProfilerCommand(IContinuousProfilingCommandTarget target, IContinuousProfilerCommandResponseFormatter responseFormatter)
    {
        Name = "stop_continuous_profiler";
        _target = target;
        _responseFormatter = responseFormatter;
    }

    public override object Process(IDictionary<string, object> arguments)
    {
        var args = new ContinuousProfilerCommandArgs(arguments);
        var result = _target.StopFromCommand(args.Include);
        return _responseFormatter.Format(result);
    }
}
