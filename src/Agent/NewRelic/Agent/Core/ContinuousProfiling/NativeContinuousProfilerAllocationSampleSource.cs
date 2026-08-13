// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Thin adapter over <see cref="INativeMethods"/> for the allocation-sampling path -- mirrors
/// <see cref="NativeContinuousProfilerSampleSource"/>'s shape for the thread-sampling path, but kept
/// as its own class since allocation sampling is independently gated (own config, own start/stop).
/// </summary>
public class NativeContinuousProfilerAllocationSampleSource : IAllocationSampleSource
{
    private readonly INativeMethods _nativeMethods;

    public NativeContinuousProfilerAllocationSampleSource(INativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods;
    }

    public void Start(int maxSamplesPerMinute) => _nativeMethods.AllocationSamplerStart(maxSamplesPerMinute);

    public void Stop() => _nativeMethods.AllocationSamplerStop();

    public void Shutdown() => _nativeMethods.AllocationSamplerShutdown();

    public int ReadBatch(byte[] destination) => _nativeMethods.ContinuousProfilerReadAllocationSamples(destination.Length, destination);
}
