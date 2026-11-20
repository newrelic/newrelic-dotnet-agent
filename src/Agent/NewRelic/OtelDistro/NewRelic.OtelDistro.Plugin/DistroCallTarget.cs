// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.OtelDistro;
using OpenTelemetry.AutoInstrumentation.CallTarget;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// The call target the generated OTel distro integration classes (namespace
/// <c>NewRelic.OtelDistro.Plugin.Generated</c>) invoke on method entry and exit. It carries the NR tracer's finish
/// delegate across the CallTarget begin/end boundary inside a <see cref="CallTargetState"/>, delegating the actual
/// tracer start/finish to Core's <see cref="DistroTracerBridge"/>.
/// </summary>
/// <remarks>
/// <c>Begin</c> has one overload per argument count (0 to 8), each generic over the calling integration class, the
/// target and the argument types, so every overload OTel passes gets its own <c>DistroMethodCache</c> entry:
/// the method that fired is resolved once and handed to Core with the call. Each overload first checks the site's gate
/// (<see cref="DistroSiteTable.IsOn"/>): a site the NR XML turns off returns an empty state before anything is resolved or
/// allocated, and <c>End</c> then has nothing to finish and returns before it touches the return value.
///
/// Log dedupe (defer-then-decide): for a gated-on log-forwarding site (<see cref="DistroSiteTable.IsLogForwarding"/>)
/// <c>Begin</c> opens a <see cref="DistroLogCapture"/> scope BEFORE the NR wrapper runs, so the wrapper's own record marks
/// the scope, and OTel's log bridge (which emits synchronously inside this call) is deferred rather than forwarded. The
/// scope is kept only when NR actually started a tracer (<see cref="DistroTracerBridge.StartedTracer"/>: a real finish,
/// not the shared no-op); otherwise it is rolled back immediately so OTel's record is forwarded. For a kept frame the
/// state's object slot carries a reusable per-thread/per-level marker (from <see cref="DistroLogCapture.CreateFrame"/>)
/// that holds this call's level and finish delegate -- no per-call allocation, and no use of the StartTime slot. <c>End</c>
/// recognises that marker (<see cref="DistroLogCapture.TryCloseFrame"/>) to close the scope and finish the tracer. Non-log
/// sites are unchanged: the object slot is the finish delegate, and <c>End</c> falls through to the normal path.
/// </remarks>
public static class DistroCallTarget
{
    public static CallTargetState Begin<TIntegration, TTarget>(int siteIndex, TTarget instance)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget>.Get(siteIndex), instance, Array.Empty<object>());
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1>(int siteIndex, TTarget instance, TArg1 arg1)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1>.Get(siteIndex), instance, new object[] { arg1 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2>.Get(siteIndex), instance, new object[] { arg1, arg2 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3, arg4 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3, arg4, arg5 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3, arg4, arg5, arg6 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    public static CallTargetState Begin<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(int siteIndex, TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8)
    {
        if (!DistroSiteTable.IsOn(siteIndex))
        {
            return new CallTargetState(null, null);
        }

        var logForwarding = DistroSiteTable.IsLogForwarding(siteIndex);
        var level = logForwarding ? DistroLogCapture.Enter() : 0;
        var finish = DistroTracerBridge.Begin(DistroSiteTable.Sites, siteIndex, DistroMethodCache<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>.Get(siteIndex), instance, new object[] { arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8 });
        return logForwarding ? BuildLogState(level, finish) : new CallTargetState(null, finish);
    }

    /// <summary>
    /// End of a non-void target. Generic so that a value-type return is boxed only when a tracer actually started: a gated
    /// off site, a site with no tracer, or a call outside a transaction returns here before the value is touched.
    /// </summary>
    public static void End<TReturn>(in CallTargetState state, TReturn returnValue, Exception exception)
    {
        if (DistroLogCapture.TryCloseFrame(state.State, out var frameFinish))
        {
            if (frameFinish != null)
            {
                DistroTracerBridge.End(frameFinish, returnValue, exception);
            }

            return;
        }

        var finish = state.State;
        if (finish == null)
        {
            return;
        }

        DistroTracerBridge.End(finish, returnValue, exception);
    }

    /// <summary>End of a void target.</summary>
    public static void End(in CallTargetState state, Exception exception)
    {
        if (DistroLogCapture.TryCloseFrame(state.State, out var frameFinish))
        {
            if (frameFinish != null)
            {
                DistroTracerBridge.End(frameFinish, null, exception);
            }

            return;
        }

        var finish = state.State;
        if (finish != null)
        {
            DistroTracerBridge.End(finish, null, exception);
        }
    }

    // A gated-on log-forwarding site. If NR actually started a tracer, keep the scope open and stash this call's level +
    // finish in a reusable per-thread/per-level marker in the state's object slot (so End closes exactly this frame). If
    // NR did not start a tracer (null or the shared no-op), roll the scope back now so OTel's record for this call is
    // forwarded, not deferred.
    private static CallTargetState BuildLogState(int level, object finish)
    {
        if (!DistroTracerBridge.StartedTracer(finish))
        {
            DistroLogCapture.ExitFrame(level);
            return new CallTargetState(null, null);
        }

        return new CallTargetState(null, DistroLogCapture.CreateFrame(level, finish));
    }
}
