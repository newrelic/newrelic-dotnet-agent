// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Samplers;

public class CpuSampler : AbstractSampler
{
    private readonly ICpuSampleTransformer _cpuSampleTransformer;

    private readonly int _processorCount;
    private DateTime _lastSampleTime;
    private TimeSpan _lastProcessorTime;
    private readonly IProcessStatic _processStatic;
    private const int CpuSampleIntervalSeconds = 60;

    public CpuSampler(IScheduler scheduler, ICpuSampleTransformer cpuSampleTransformer, IProcessStatic processStatic)
        : base(scheduler, TimeSpan.FromSeconds(CpuSampleIntervalSeconds))
    {
        _cpuSampleTransformer = cpuSampleTransformer;
        _processStatic = processStatic;

        try
        {
            _processorCount = System.Environment.ProcessorCount;
            _lastSampleTime = DateTime.UtcNow;
            _lastProcessorTime = GetCurrentUserProcessorTime();
        }
        catch (Exception ex)
        {
            Log.Error($"Unable to get CPU sample.  No CPU metrics will be reported.  Error : {ex}");
            Stop();
        }
    }

    public override void Sample()
    {
        try
        {
            var currentSampleTime = DateTime.UtcNow;
            var currentProcessorTime = GetCurrentUserProcessorTime();
            var immutableCpuSample = new ImmutableCpuSample(_processorCount, _lastSampleTime, _lastProcessorTime, currentSampleTime, currentProcessorTime);
            _cpuSampleTransformer.Transform(immutableCpuSample);
            _lastSampleTime = currentSampleTime;
            _lastProcessorTime = currentProcessorTime;
        }
        catch (Exception ex)
        {
            Log.Error($"Unable to get CPU sample.  No CPU metrics will be reported.  Error : {ex}");
            Stop();
        }
    }

    private TimeSpan GetCurrentUserProcessorTime()
    {
        var process = _processStatic.GetCurrentProcess();
        process.Refresh();
        return process.UserProcessorTime;
    }
}

public class ImmutableCpuSample
{
    public readonly int ProcessorCount;

    public readonly DateTime LastSampleTime;

    public readonly TimeSpan LastUserProcessorTime;

    public readonly DateTime CurrentSampleTime;

    public readonly TimeSpan CurrentUserProcessorTime;

    public ImmutableCpuSample(int processorCount, DateTime lastSampleTime, TimeSpan lastUserProcessorTime, DateTime currentSampleTime, TimeSpan currentUserProcessorTime)
    {
        ProcessorCount = processorCount;
        LastSampleTime = lastSampleTime;
        LastUserProcessorTime = lastUserProcessorTime;
        CurrentSampleTime = currentSampleTime;
        CurrentUserProcessorTime = currentUserProcessorTime;
    }
}
