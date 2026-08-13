// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Builds the continuous-profiling object graph for <see cref="AgentManager"/>. Construction is always
/// attempted regardless of configuration, matching the convention every other native-adjacent service in
/// the agent follows (<c>ThreadProfilingService</c>, <c>SamplerFactory</c>, the GC/CPU/memory samplers,
/// <c>MeterListenerBridge</c>): construct unconditionally, defer the risky/expensive work to a method
/// invoked later, and react to live config via <c>ConfigurationBasedService.OnConfigurationUpdated</c>.
/// Nothing built here touches native code -- <see cref="NativeContinuousProfilerSampleSource"/>'s ctor
/// just stores an <see cref="INativeMethods"/> reference, and .NET does not bind a P/Invoke's native
/// entry point until the method is actually called, which only happens inside
/// <see cref="ContinuousProfilingService"/>'s own try/catch around <c>_native.Start</c>. The try/catch
/// below is cheap insurance around construction, not a safety net for a known risk.
/// </summary>
public static class ContinuousProfilingServiceFactory
{
    /// <summary>
    /// Returns a constructed <see cref="ContinuousProfilingService"/>, or <c>null</c> if construction
    /// failed. Callers must treat a null result as "no continuous profiling for the lifetime of this
    /// process."
    /// </summary>
    public static ContinuousProfilingService TryCreate(IContainer container, IConfiguration configuration, IAgentHealthReporter agentHealthReporter)
    {
        try
        {
            // Plan B wires the native sampler-backed source, which both drives the native lifecycle
            // (INativeContinuousProfiler) and drains its buffers (ISampleSource) -- one object, passed for
            // both seams. The OTLP/HTTP dispatch is wired REAL (api-key protobuf POST); no endpoint is
            // known yet -- it's resolved from the collector's connection once the agent connects
            // (ContinuousProfilingService.OnAgentConnected); drains before that point are dropped without
            // doing any work (see DrainOnce).
            var nativeMethods = container.Resolve<INativeMethods>();
            var profilesDispatcher = new OtlpProfilesHttpDispatcher(configuration);
            var profilesTransport = new ProfilesTransport(profilesDispatcher.Post, null, agentHealthReporter);
            var sampleSource = new NativeContinuousProfilerSampleSource(nativeMethods);

            // The allocation sampler is a SEPARATE native object with its own EventPipe session and buffer
            // queue, so it gets its own adapter here rather than sharing the thread sampler's. Constructed
            // unconditionally for the same reason everything else here is: it only stores the INativeMethods
            // reference, and no P/Invoke is bound until ContinuousProfilingService actually starts it (which it
            // does only when allocation sampling is configured on).
            var allocationSampleSource = new NativeContinuousProfilerAllocationSampleSource(nativeMethods);

            return new ContinuousProfilingService(sampleSource, sampleSource, allocationSampleSource, profilesTransport, container.Resolve<IScheduler>(), agentHealthReporter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to construct the continuous profiling service; continuous profiling will be unavailable for this process.");
            return null;
        }
    }
}
