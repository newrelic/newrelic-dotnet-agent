// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Translates the start_continuous_profiler/stop_continuous_profiler "include" tokens ("all", "cpu", "heap")
/// into the two things this agent can actually act on today: whether to touch the cpu+off_cpu sampler bundle
/// (the only thing <see cref="ContinuousProfilingService"/> can start/stop right now -- on/off-CPU
/// classification is a per-sample attribute produced by a single sampler, not two independently toggleable
/// modes), and whether "heap" (allocation profiling) was requested -- which always reports "not supported"
/// since allocation profiling isn't implemented yet. Per the spec: "all" = cpu + off_cpu + allocations;
/// "cpu" = cpu + off_cpu only; "heap" = allocations only.
/// </summary>
public static class ContinuousProfilingCommandTypes
{
    public const string All = "all";
    public const string Cpu = "cpu";
    public const string Heap = "heap";

    /// <summary>
    /// Classifies one "include" token. <paramref name="startsCpuBundle"/> is true for "all"/"cpu";
    /// <paramref name="requestsHeap"/> is true for "all"/"heap"; an unrecognized token sets neither, and
    /// the caller reports it verbatim as an unsupported type.
    /// </summary>
    public static void Classify(string token, out bool startsCpuBundle, out bool requestsHeap)
    {
        switch (token)
        {
            case All:
                startsCpuBundle = true;
                requestsHeap = true;
                break;
            case Cpu:
                startsCpuBundle = true;
                requestsHeap = false;
                break;
            case Heap:
                startsCpuBundle = false;
                requestsHeap = true;
                break;
            default:
                startsCpuBundle = false;
                requestsHeap = false;
                break;
        }
    }
}
