// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Translates the start_continuous_profiler/stop_continuous_profiler "include" tokens ("all", "cpu", "heap")
/// into the two samplers <see cref="ContinuousProfilingService"/> can independently start and stop: the
/// cpu+off_cpu bundle (one bundle, not two toggles -- on/off-CPU classification is a per-sample attribute
/// produced by a single sampler) and the allocation sampler. Both are now really acted on: "heap" starts and
/// stops allocation sampling under the same command-ownership rules as "cpu". Per the spec:
/// "all" = cpu + off_cpu + allocations; "cpu" = cpu + off_cpu only; "heap" = allocations only. Only an
/// unrecognized token is reported back as "not supported".
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
