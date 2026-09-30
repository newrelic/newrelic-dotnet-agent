// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using NewRelic.Api.Agent;

namespace ContainerizedAspNetCoreApp.Controllers;

/// <summary>
/// Drives the continuous-profiling correlation path for the Linux container test: BurnCpu does synchronous
/// CPU work on the same (traced) request thread, never handed off to a worker, so the sampler reliably
/// captures an active trace/span for correlation. See ContinuousProfilingContainerTest for why.
/// </summary>
[ApiController]
[Route("[controller]")]
public class ContinuousProfilingController : ControllerBase
{
    [HttpGet("burncpu")]
    public string BurnCpu([FromQuery] int seconds = 8)
    {
        var iterations = BurnCpuSynchronously(seconds);
        return $"burned cpu for {seconds}s ({iterations} iterations)";
    }

    // Synchronous, inline CPU work on the request (traced) thread. [Trace] + NoInlining/NoOptimization
    // ensures the profiler actually instruments this method and it stays on the stack long enough to be
    // sampled with the active trace context (matching the Windows correlation exerciser's pattern).
    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private static long BurnCpuSynchronously(int seconds)
    {
        var stopwatch = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(Math.Max(1, seconds));
        long accumulator = 0;

        // Tight arithmetic loop that keeps the thread on-CPU (not sleeping/blocking) so stack snapshots
        // taken during this window show this frame with the transaction's trace/span context attached.
        while (stopwatch.Elapsed < deadline)
        {
            for (var i = 0; i < 1_000_000; i++)
            {
                accumulator += i * 31 + (accumulator & 0xFF);
            }
        }

        return accumulator;
    }
}
