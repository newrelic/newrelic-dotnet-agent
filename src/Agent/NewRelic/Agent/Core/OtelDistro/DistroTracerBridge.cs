// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Tracer;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Thin bridge that generated OTel distro integrations (a Roslyn source generator, and the distro plugin)
/// call into to start and finish a tracer through <see cref="AgentShim"/>, without the generated code or
/// the plugin needing to know about <see cref="AgentShim"/>'s parameter shape directly.
/// </summary>
public static class DistroTracerBridge
{
    /// <summary>
    /// Starts a tracer for the site at <paramref name="siteIndex"/> using the method the plugin resolved for this
    /// instantiation: its real declaring type, argument signature and function id, and the async bit when it is async.
    /// Returns null (no tracer, never an exception) for null <paramref name="sites"/>, an index naming no site, or a
    /// null <paramref name="resolved"/>.
    /// </summary>
    public static Action<object, Exception> Begin(DistroInstrumentationSites sites, int siteIndex, DistroResolvedMethod resolved, object instance, object[] arguments)
    {
        if (resolved == null || sites == null || !sites.TryGet(siteIndex, out var site))
        {
            return null;
        }

        var tracerArguments = resolved.IsAsync ? site.TracerArguments | (uint)TracerFlags.Async : site.TracerArguments;
        return Begin(site, tracerArguments, resolved.DeclaringType, resolved.ArgumentSignature, resolved.FunctionId, instance, arguments);
    }

    private static Action<object, Exception> Begin(DistroInstrumentationSite site, uint tracerArguments, Type targetType, string argumentSignature, ulong functionId, object instance, object[] arguments)
    {
        try
        {
            return AgentShim.GetFinishTracerDelegate(
                site.TracerFactoryName,
                tracerArguments,
                site.MetricName,
                site.AssemblyName,
                targetType,
                site.TypeName,
                site.MethodName,
                argumentSignature,
                instance,
                arguments ?? new object[0],
                functionId,
                null);
        }
        // Defense-in-depth: never throw into the instrumented method. Unreachable through current inputs (the site comes
        // from the table, and AgentShim.GetTracer swallows its own failures), so no test reaches it.
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="finish"/> is a real tracer finish: not null and not <see cref="AgentShim"/>'s shared
    /// no-op (which <see cref="AgentShim.GetFinishTracerDelegate"/> returns, never null, when no tracer started -- agent
    /// not registered, or no tracer factory match). The OTel distro log dedupe keeps a capture scope open only when a
    /// real tracer started, so a call where New Relic records nothing has its OTel copy forwarded.
    /// </summary>
    public static bool StartedTracer(object finish) => finish != null && !AgentShim.IsNoOpFinishTracer(finish);

    public static void End(object finishDelegate, object returnValue, Exception exception)
    {
        try
        {
            (finishDelegate as Action<object, Exception>)?.Invoke(returnValue, exception);
        }
        catch (Exception)
        {
        }
    }
}
