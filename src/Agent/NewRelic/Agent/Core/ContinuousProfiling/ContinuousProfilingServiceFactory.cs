// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
#endif
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.DependencyInjection;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.ContinuousProfiling;

/// <summary>
/// Builds the continuous-profiling object graph for <see cref="AgentManager"/>. On .NET Core, construction
/// is always attempted regardless of configuration, matching every other native-adjacent service in the
/// agent: construct unconditionally, defer risky/expensive work to a method invoked later. Nothing built
/// here touches native code -- .NET does not bind a P/Invoke's native entry point until the method is
/// actually called, which only happens inside <see cref="ContinuousProfilingService"/>'s own try/catch
/// around <c>_native.Start</c>. On .NET Framework this always returns <c>null</c>: the native profiler is
/// a process-wide singleton but the managed agent is per-AppDomain, and a shared IIS app pool can host
/// multiple AppDomains in one process -- continuous profiling is not supported there yet.
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
#if NETFRAMEWORK
        Log.Info("[ContinuousProfiling] Continuous profiling is not supported on .NET Framework; continuous profiling will be unavailable for this process.");
        return null;
#else
        try
        {
            // The native sampler-backed source drives both the native lifecycle (INativeContinuousProfiler)
            // and buffer draining (ISampleSource) -- one object, passed for both seams. The OTLP endpoint
            // isn't known yet; it's resolved from the collector connection once the agent connects, and
            // drains before that point are dropped without doing any work (see DrainOnce).
            var nativeMethods = container.Resolve<INativeMethods>();
            var supportabilityMetricCounters = container.Resolve<IContinuousProfilingSupportabilityMetricCounters>();
            var profilesDispatcher = new OtlpProfilesHttpDispatcher(configuration, supportabilityMetricCounters);
            var profilesTransport = new ProfilesTransport(profilesDispatcher.Post, null, agentHealthReporter, supportabilityMetricCounters);
            var sampleSource = new NativeContinuousProfilerSampleSource(nativeMethods);

            return new ContinuousProfilingService(sampleSource, sampleSource, profilesTransport, container.Resolve<IScheduler>(), agentHealthReporter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to construct the continuous profiling service; continuous profiling will be unavailable for this process.");
            return null;
        }
#endif
    }
}
