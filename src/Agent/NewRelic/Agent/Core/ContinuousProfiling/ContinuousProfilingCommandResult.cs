// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// The outcome of applying a start_continuous_profiler/stop_continuous_profiler agent command, independent
/// of how it's eventually serialized into the command response (see
/// NewRelic.Agent.Core.Commands.IContinuousProfilerCommandResponseFormatter). Kept response-shape-agnostic
/// on purpose: the DACI's proposed detailed-response section (include/sample_interval/cpu_report_interval/
/// exceptions) is still undecided, and the plain-ack path needs none of these fields -- so this type is the
/// single source of truth both formatters read from, and neither formatter needs to know how
/// ContinuousProfilingService computed it.
/// </summary>
public class ContinuousProfilingCommandResult
{
    /// <summary>Profile-type tokens ("cpu") currently profiling, after this command was applied.</summary>
    public IReadOnlyList<string> ActiveTypes { get; }

    public int SampleIntervalMs { get; }

    public int CpuReportIntervalMs { get; }

    /// <summary>
    /// Profile-type token (or unrecognized token) -> reason it could not be started/stopped, e.g.
    /// "heap" -> "not supported". Empty when nothing requested was unsupported.
    /// </summary>
    public IReadOnlyDictionary<string, string> Exceptions { get; }

    public ContinuousProfilingCommandResult(IReadOnlyList<string> activeTypes, int sampleIntervalMs, int cpuReportIntervalMs, IReadOnlyDictionary<string, string> exceptions)
    {
        ActiveTypes = activeTypes;
        SampleIntervalMs = sampleIntervalMs;
        CpuReportIntervalMs = cpuReportIntervalMs;
        Exceptions = exceptions;
    }
}
