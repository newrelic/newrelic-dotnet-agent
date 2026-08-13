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
/// Builds the continuous-profiling object graph for <see cref="AgentManager"/>. Continuous profiling is
/// a prototype feature that is off by default, and <c>AgentManager</c>'s own construction is wrapped in
/// a catch-all that swaps in the <c>DisabledAgentManager</c> -- so an unguarded throw from anything in
/// this graph (e.g. a P/Invoke resolution failure resolving <see cref="INativeMethods"/>) would cost a
/// customer who never opted in their entire telemetry pipeline. Hence: gate on config first, then
/// catch-log-continue so the worst case degrades to "no continuous profiling" instead of "no agent."
/// </summary>
public static class ContinuousProfilingServiceFactory
{
    /// <summary>
    /// Returns a constructed <see cref="ContinuousProfilingService"/>, or <c>null</c> when continuous
    /// profiling is disabled by configuration or its construction failed. Callers must treat a null
    /// result as "no continuous profiling for the lifetime of this process."
    /// </summary>
    public static ContinuousProfilingService TryCreate(IContainer container, IConfiguration configuration, IAgentHealthReporter agentHealthReporter)
    {
        if (!configuration.ContinuousProfilingEnabled)
            return null;

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

            return new ContinuousProfilingService(sampleSource, sampleSource, profilesTransport, container.Resolve<IScheduler>(), agentHealthReporter);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ContinuousProfiling] Failed to construct the continuous profiling service; continuous profiling will be unavailable for this process.");
            return null;
        }
    }
}
