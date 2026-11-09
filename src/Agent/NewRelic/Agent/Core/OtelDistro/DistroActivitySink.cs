// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.OtelDistro;

/// <summary>
/// Feeds OpenTelemetry Activities (passed as <see cref="object"/>, since the caller's DiagnosticSource types
/// are not Core's internalized copy) into the existing <see cref="ActivityBridge"/> transform, without an
/// <c>ActivityListener</c> owned by Core. Used by the OTel auto-instrumentation distro plugin.
/// </summary>
public class DistroActivitySink
{
    private readonly IAgent _agent;
    private readonly IErrorService _errorService;

    // F59 takeover links, set when a duplicate is collapsed: duplicate -> framework Activity (to carry its data over at
    // the duplicate's stop) and framework Activity -> duplicate (to finish the request if the duplicate never stops).
    private const string FrameworkActivityPropertyName = "NewRelic.Distro.FrameworkActivity";
    private const string DuplicateActivityPropertyName = "NewRelic.Distro.DuplicateServerActivity";

    // F59. The duplicate starts synchronously right after the first, in the same flow (upstream creates it from the
    // first's HttpRequestIn.Start diagnostic event), so the last Server + remote-parent Activity started in this
    // ExecutionContext is the only candidate. Async-local, not thread-static: another request on the same thread runs
    // in its own ExecutionContext and cannot see it. Weak, so a request that never gets a duplicate does not keep its
    // Activity (and, through the segment custom property, its transaction) alive.
    private readonly AsyncLocal<WeakReference<object>> _lastServerActivityWithRemoteParent = new();

    // Compiled accessors for the caller's Activity type, built on first use (one Activity type per process in practice).
    private volatile ActivityAccessors _accessors;

    // Set once the agent shuts down (incl. KillAgentEvent paths). The caller's listener outlives the agent, so every
    // callback checks this first: one volatile read on the per-Activity path.
    private volatile bool _disabled;

    private int _loggedShouldListenToError;
    private int _loggedShouldSampleError;
    private int _loggedOnStartedError;
    private int _loggedOnStoppedError;

    /// <summary>
    /// Whether the caller should attach its listener at all. Mirrors agent mode, which starts the ActivityBridge only
    /// when OpenTelemetry is enabled (AgentManager) and OpenTelemetry tracing is enabled (ActivityBridge.Start).
    /// False once <see cref="Disable"/> has been called.
    /// </summary>
    public bool TracingEnabled
    {
        get
        {
            if (_disabled)
            {
                return false;
            }

            var configuration = _agent.Configuration;
            return configuration.OpenTelemetryEnabled && configuration.OpenTelemetryTracingEnabled;
        }
    }

    /// <summary>
    /// Whether <see cref="Disable"/> has been called.
    /// </summary>
    public bool IsDisabled => _disabled;

    public DistroActivitySink(IAgent agent, IErrorService errorService)
    {
        _agent = agent;
        _errorService = errorService;
    }

    /// <summary>
    /// Stops feeding Core: afterwards nothing is listened to or sampled and started/stopped Activities are ignored.
    /// Called when the agent shuts down. Irreversible and idempotent.
    /// </summary>
    public void Disable()
    {
        _disabled = true;
    }

    /// <summary>
    /// Prepares the reflection-built helpers the bridge needs. The types are passed in (from the caller's
    /// DiagnosticSource) rather than referenced, so Core never exposes DiagnosticSource types publicly.
    /// Does not set Activity.DefaultIdFormat or Activity.TraceIdGenerator.
    /// </summary>
    public void InitializeActivityTypes(Type activityType, Type activitySourceType, Type activityKindType)
    {
        ActivityBridge.CreateActivityTraceFlagsSetter(activityType);
        NewRelicActivitySourceProxy.SetAndCreateRuntimeActivitySource(activitySourceType, activityKindType);
    }

    public bool ShouldListenTo(string activitySourceName)
    {
        if (_disabled || string.IsNullOrWhiteSpace(activitySourceName))
        {
            return false;
        }

        try
        {
            return ActivitySourceExtensions.ShouldListenToActivitySource(activitySourceName, _agent.Configuration);
        }
        catch (Exception exception)
        {
            LogCallbackErrorOnce(ref _loggedShouldListenToError, exception, nameof(ShouldListenTo));
            return false;
        }
    }

    // Mirrors ActivityBridge.ShouldSampleActivity, but takes the parent's remote flag directly (no dynamic).
    public bool ShouldSample(int activityKind, bool parentIsRemote)
    {
        if (_disabled)
        {
            return false;
        }

        try
        {
            // If there is a transaction already in progress, we should sample the activity.
            var transaction = _agent.CurrentTransaction;
            if (transaction.IsValid && !transaction.IsFinished)
            {
                return true;
            }

            var kind = (ActivityKind)activityKind;
            return parentIsRemote || kind == ActivityKind.Server || kind == ActivityKind.Consumer;
        }
        catch (Exception exception)
        {
            LogCallbackErrorOnce(ref _loggedShouldSampleError, exception, nameof(ShouldSample));
            return false;
        }
    }

    public void OnStarted(object activity)
    {
        if (_disabled)
        {
            return;
        }

        if (TryCollapseDuplicateServerActivity(activity))
        {
            return;
        }

        ActivityBridge.ActivityStarted(activity, _agent);
    }

    /// <summary>
    /// Whether <paramref name="second"/> duplicates the still-running <paramref name="first"/>: both are Server
    /// Activities with a remote parent, with the same TraceId and the same remote ParentSpanId (F59: upstream's
    /// ASP.NET Core instrumentation starts a sibling of the framework's HttpRequestIn Activity on propagated requests).
    /// </summary>
    public bool IsDuplicateServerActivity(object first, object second)
    {
        if (first == null || ReferenceEquals(first, second))
        {
            return false;
        }

        var accessors = GetAccessors(second);
        return accessors.IsServerWithRemoteParent(first)
            && accessors.IsServerWithRemoteParent(second)
            && !accessors.IsStopped(first)
            && accessors.HasSameTraceIdAndParentSpanId(first, second);
    }

    // Returns true when the Activity was collapsed into the first one's segment, so the bridge must not see it.
    // Keeps ONE NR server segment (the first's: it started the transaction and accepted the inbound headers) and
    // re-binds it to the duplicate, which is Activity.Current for the rest of the request: child Activities and the
    // outbound traceparent then carry the duplicate's span id, which is now that NR segment's span id.
    private bool TryCollapseDuplicateServerActivity(object activity)
    {
        ActivityAccessors accessors;
        Segment segment;
        object first;
        try
        {
            accessors = GetAccessors(activity);
            if (!accessors.IsServerWithRemoteParent(activity))
            {
                return false;
            }

            first = null;
            if (_lastServerActivityWithRemoteParent.Value?.TryGetTarget(out first) != true
                || !IsDuplicateServerActivity(first, activity)
                || RuntimeNewRelicActivity.GetSegmentFromActivity(first) is not Segment { IsDone: false } firstSegment)
            {
                _lastServerActivityWithRemoteParent.Value = new WeakReference<object>(activity);
                return false;
            }

            _lastServerActivityWithRemoteParent.Value = null;
            segment = firstSegment;
        }
        catch (Exception exception)
        {
            LogCallbackErrorOnce(ref _loggedOnStartedError, exception, nameof(OnStarted));
            return false;
        }

        // From here on the segment is being re-bound: never fall back to the bridge, which would start a second
        // segment with the same span id.
        try
        {
            var duplicate = new RuntimeNewRelicActivity(activity);
            segment.SpanId = accessors.SpanIdHex(activity);
            segment.SetActivity(duplicate);
            duplicate.SetSegment(segment);
            new RuntimeNewRelicActivity(first).SetSegment(null);
            accessors.SetCustomProperty(activity, FrameworkActivityPropertyName, first);
            accessors.SetCustomProperty(first, DuplicateActivityPropertyName, activity);

            // Same as the bridge does on start: the trace flags and tracestate NR's data model expects. The tracestate
            // is rebuilt so it names the current segment's span id, which is now the duplicate's.
            if (segment.GetTransactionFromSegment() is IHybridAgentTransaction transaction
                && transaction.TryGetTraceFlagsAndState(out var sampled, out var traceStateString))
            {
                accessors.SetTraceStateString(activity, traceStateString);
                accessors.SetSampled(activity, sampled);
            }
        }
        catch (Exception exception)
        {
            LogCallbackErrorOnce(ref _loggedOnStartedError, exception, nameof(OnStarted));
        }

        return true;
    }

    public void OnStopped(object activity)
    {
        if (_disabled)
        {
            return;
        }

        HandleCollapsedServerActivityStop(activity);

        ActivityBridge.ActivityStopped(activity, _agent, _errorService);
    }

    // F59 takeover, stop side. Only Server Activities can be part of a takeover, so others cost one compiled Kind read.
    private void HandleCollapsedServerActivityStop(object activity)
    {
        try
        {
            var accessors = GetAccessors(activity);
            if (accessors.Kind(activity) != (int)ActivityKind.Server)
            {
                return;
            }

            if (accessors.GetCustomProperty(activity, FrameworkActivityPropertyName) is { } framework)
            {
                // The duplicate is stopping and the bridge is about to end the segment from it: first carry over what
                // was recorded on the framework Activity, which has no segment of its own any more.
                CarryOverFrameworkActivityData(framework, activity);
            }
            else if (accessors.GetCustomProperty(activity, DuplicateActivityPropertyName) is { } duplicate && !accessors.IsStopped(duplicate))
            {
                // The framework Activity is stopping but its owner never stopped the duplicate (upstream stops it only
                // if it is still Activity.Current at HttpRequestIn.Stop). Stop it, so the segment and the transaction
                // end through the normal path (this re-enters OnStopped for the duplicate).
                accessors.Stop(duplicate);
            }
        }
        catch (Exception exception)
        {
            LogCallbackErrorOnce(ref _loggedOnStoppedError, exception, nameof(OnStopped));
        }
    }

    // Once per collapsed request, so plain dynamic. The duplicate's own values win: the framework Activity only adds
    // the tags the duplicate lacks, its events, and its status when the duplicate has none.
    private static void CarryOverFrameworkActivityData(object framework, object duplicate)
    {
        dynamic dynamicFramework = framework;
        dynamic dynamicDuplicate = duplicate;

        foreach (var tag in (IEnumerable<KeyValuePair<string, object>>)dynamicFramework.TagObjects)
        {
            if (dynamicDuplicate.GetTagItem(tag.Key) == null)
            {
                dynamicDuplicate.SetTag(tag.Key, tag.Value);
            }
        }

        foreach (var activityEvent in (IEnumerable)dynamicFramework.Events)
        {
            dynamicDuplicate.AddEvent((dynamic)activityEvent);
        }

        if ((int)dynamicDuplicate.Status == (int)ActivityStatusCode.Unset && (int)dynamicFramework.Status != (int)ActivityStatusCode.Unset)
        {
            dynamicDuplicate.SetStatus(dynamicFramework.Status, (string)dynamicFramework.StatusDescription);
        }
    }

    private ActivityAccessors GetAccessors(object activity)
    {
        var activityType = activity.GetType();
        var accessors = _accessors;
        if (accessors == null || accessors.ActivityType != activityType)
        {
            accessors = new ActivityAccessors(activityType);
            _accessors = accessors;
        }

        return accessors;
    }

    // Typed delegates over the caller's Activity type (not Core's internalized DiagnosticSource), so the per-Activity
    // checks avoid dynamic. Needs DiagnosticSource 7+ (IsStopped), the same minimum the bridge already requires.
    private sealed class ActivityAccessors
    {
        public readonly Type ActivityType;
        public readonly Func<object, int> Kind;
        public readonly Func<object, bool> HasRemoteParent;
        public readonly Func<object, bool> IsStopped;
        public readonly Func<object, object, bool> HasSameTraceIdAndParentSpanId;
        public readonly Func<object, string> SpanIdHex;
        public readonly Func<object, string, object> GetCustomProperty;
        public readonly Action<object, string, object> SetCustomProperty;
        public readonly Action<object, string> SetTraceStateString;
        public readonly Action<object, bool> SetSampled;
        public readonly Action<object> Stop;

        public ActivityAccessors(Type activityType)
        {
            ActivityType = activityType;

            var activity = Expression.Parameter(typeof(object), "activity");
            var other = Expression.Parameter(typeof(object), "other");
            var name = Expression.Parameter(typeof(string), "name");
            var value = Expression.Parameter(typeof(object), "value");
            var stringValue = Expression.Parameter(typeof(string), "stringValue");
            var sampled = Expression.Parameter(typeof(bool), "sampled");
            var typed = Expression.Convert(activity, activityType);
            var typedOther = Expression.Convert(other, activityType);

            Kind = Expression.Lambda<Func<object, int>>(Expression.Convert(Expression.Property(typed, "Kind"), typeof(int)), activity).Compile();
            HasRemoteParent = Expression.Lambda<Func<object, bool>>(Expression.Property(typed, "HasRemoteParent"), activity).Compile();
            IsStopped = Expression.Lambda<Func<object, bool>>(Expression.Property(typed, "IsStopped"), activity).Compile();
            HasSameTraceIdAndParentSpanId = Expression.Lambda<Func<object, object, bool>>(
                Expression.AndAlso(
                    Expression.Equal(Expression.Property(typed, "TraceId"), Expression.Property(typedOther, "TraceId")),
                    Expression.Equal(Expression.Property(typed, "ParentSpanId"), Expression.Property(typedOther, "ParentSpanId"))),
                activity, other).Compile();
            SpanIdHex = Expression.Lambda<Func<object, string>>(Expression.Call(Expression.Property(typed, "SpanId"), "ToHexString", Type.EmptyTypes), activity).Compile();
            GetCustomProperty = Expression.Lambda<Func<object, string, object>>(Expression.Call(typed, "GetCustomProperty", Type.EmptyTypes, name), activity, name).Compile();
            SetCustomProperty = Expression.Lambda<Action<object, string, object>>(Expression.Call(typed, "SetCustomProperty", Type.EmptyTypes, name, value), activity, name, value).Compile();
            SetTraceStateString = Expression.Lambda<Action<object, string>>(Expression.Assign(Expression.Property(typed, "TraceStateString"), stringValue), activity, stringValue).Compile();

            // Same assignment as ActivityBridge.CreateActivityTraceFlagsSetter: Recorded when sampled, else None.
            var traceFlags = Expression.Property(typed, "ActivityTraceFlags");
            var traceFlagsValue = Expression.Convert(
                Expression.Condition(sampled, Expression.Constant((int)ActivityTraceFlags.Recorded), Expression.Constant((int)ActivityTraceFlags.None)),
                traceFlags.Type);
            SetSampled = Expression.Lambda<Action<object, bool>>(Expression.Assign(traceFlags, traceFlagsValue), activity, sampled).Compile();

            Stop = Expression.Lambda<Action<object>>(Expression.Call(typed, "Stop", Type.EmptyTypes), activity).Compile();
        }

        public bool IsServerWithRemoteParent(object activity) => Kind(activity) == (int)ActivityKind.Server && HasRemoteParent(activity);
    }

    // Same pattern as ActivityBridge.LogCallbackErrorOnce: a repeating fault would otherwise flood the log on every
    // source or Activity. Per instance, so the distro sink's flags are independent of agent mode's.
    private static void LogCallbackErrorOnce(ref int loggedFlag, Exception exception, string callbackName)
    {
        if (Interlocked.Exchange(ref loggedFlag, 1) != 0)
        {
            return;
        }

        Log.Error(exception, $"OTel distro activity sink {callbackName} callback failed. Later {callbackName} failures will not be logged.");
    }
}
