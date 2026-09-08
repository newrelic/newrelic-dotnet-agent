// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ContinuousProfiling;

namespace NewRelic.Agent.Core.Commands;

public class StartContinuousProfilerCommand : AbstractCommand
{
    private readonly IContinuousProfilingCommandTarget _target;
    private readonly IContinuousProfilerCommandResponseFormatter _responseFormatter;

    public StartContinuousProfilerCommand(IContinuousProfilingCommandTarget target, IContinuousProfilerCommandResponseFormatter responseFormatter)
    {
        Name = "start_continuous_profiler";
        _target = target;
        _responseFormatter = responseFormatter;
    }

    public override object Process(IDictionary<string, object> arguments)
    {
        var args = new ContinuousProfilerCommandArgs(arguments);
        var result = _target.StartFromCommand(args.Include, args.SampleIntervalMs, args.CpuReportIntervalMs);
        return _responseFormatter.Format(result);
    }
}
