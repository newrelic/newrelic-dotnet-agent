// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using NewRelic.Agent.Core.OtelDistro;

namespace NewRelic.OtelDistro.Plugin;

/// <summary>
/// Owns the typed <c>ActivityListener</c> that routes the application's own (non-internalized) Activities
/// into Core's <see cref="DistroActivitySink"/>.
/// </summary>
public sealed class DistroActivityListener : IDisposable
{
    private readonly DistroActivitySink _sink;
    private ActivityListener _listener;

    // Activity.TraceIdGenerator is Func<ActivityTraceId>. The ILRepacked Core leaks its own public copy of
    // ActivityTraceId, so naming the type here is ambiguous (CS0433) exactly like ActivityKind in Start(). Unlike
    // ActivityKind (passed to Core as a Type), the generator must *return* the application's ActivityTraceId, and its
    // factory ActivityTraceId.CreateFromString takes a ReadOnlySpan<char> (a ref struct) that neither MethodInfo.Invoke
    // nor expression trees can pass. So the generator is built once as a DynamicMethod and installed/restored through
    // the Activity.TraceIdGenerator property by reflection, never naming ActivityTraceId in source.
    private PropertyInfo _traceIdGeneratorProperty;
    private Delegate _previousTraceIdGenerator;
    private bool _traceIdGeneratorInstalled;

    public DistroActivityListener(DistroActivitySink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public void Start()
    {
        if (_listener != null)
        {
            return;
        }

        // Mirrors agent mode (ActivityBridge.SetDefaultActivityIdFormat): W3C default only, no ForceDefaultIdFormat.
        // Without it root activities on .NET Framework use the Hierarchical id format and get all-zero trace ids.
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;

        // The ILRepacked Core leaks its own public copy of ActivityKind, so naming the type here is ambiguous (CS0433).
        // Resolve the application's DiagnosticSource ActivityKind through the assembly that owns ActivitySource instead.
        var activityKindType = typeof(ActivitySource).Assembly.GetType("System.Diagnostics.ActivityKind", throwOnError: true);
        _sink.InitializeActivityTypes(typeof(Activity), typeof(ActivitySource), activityKindType);

        // Every root Activity created inside an NR-wrapper transaction adopts that transaction's NR trace id, so a
        // single-segment NR transaction and the application's Activity.Current agree on the trace id from the start.
        InstallTraceIdGenerator();

        var listener = new ActivityListener
        {
            ShouldListenTo = source => _sink.ShouldListenTo(source.Name),
            Sample = Sample,
            ActivityStarted = _sink.OnStarted,
            ActivityStopped = _sink.OnStopped
        };

        ActivitySource.AddActivityListener(listener);
        _listener = listener;
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _listener = null;

        if (_traceIdGeneratorInstalled)
        {
            _traceIdGeneratorProperty.SetValue(null, _previousTraceIdGenerator);
            _traceIdGeneratorInstalled = false;
        }
    }

    // Same results as agent mode (ActivityBridge.ConfigureSampleCallback): AllDataAndRecorded or None.
    private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
    {
        return _sink.ShouldSample((int)options.Kind, options.Parent.IsRemote)
            ? ActivitySamplingResult.AllDataAndRecorded
            : ActivitySamplingResult.None;
    }

    private void InstallTraceIdGenerator()
    {
        // The application's DiagnosticSource ActivityTraceId, resolved through the assembly that owns Activity so the
        // leaked Core copy is never referenced.
        var activityTraceIdType = typeof(Activity).Assembly.GetType("System.Diagnostics.ActivityTraceId", throwOnError: true);
        var funcType = typeof(Func<>).MakeGenericType(activityTraceIdType);

        _traceIdGeneratorProperty = typeof(Activity).GetProperty("TraceIdGenerator", BindingFlags.Public | BindingFlags.Static);
        _previousTraceIdGenerator = (Delegate)_traceIdGeneratorProperty.GetValue(null);

        var generator = BuildTraceIdGenerator(activityTraceIdType, funcType);
        _traceIdGeneratorProperty.SetValue(null, generator);
        _traceIdGeneratorInstalled = true;
    }

    // Builds the Func<ActivityTraceId> generator as IL: call the (never-throwing) string helper; on a validated id
    // return ActivityTraceId.CreateFromString(id.AsSpan()), otherwise ActivityTraceId.CreateRandom(). The generator
    // runs inside every root Activity creation in the process, so it must never throw -- CreateFromString is only
    // reached with a 32-char lower-hex id it cannot reject, and any failure in the helper yields a random id.
    private Delegate BuildTraceIdGenerator(Type activityTraceIdType, Type funcType)
    {
        // ReadOnlySpan<char> is itself leaked by the ILRepacked Core (CS0433 on net462), so derive the span type from
        // AsSpan's return type rather than naming it. MemoryExtensions resolves unambiguously to System.Memory here.
        var asSpan = typeof(MemoryExtensions).GetMethod(nameof(MemoryExtensions.AsSpan), new[] { typeof(string) });
        var spanType = asSpan.ReturnType;
        var createFromString = activityTraceIdType.GetMethod("CreateFromString", BindingFlags.Public | BindingFlags.Static, null, new[] { spanType }, null);
        var createRandom = activityTraceIdType.GetMethod("CreateRandom", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        var getValidatedId = typeof(DistroActivityListener).GetMethod(nameof(GetValidatedTraceId), BindingFlags.NonPublic | BindingFlags.Instance);

        var method = new DynamicMethod(
            "DistroGenerateTraceId",
            activityTraceIdType,
            new[] { typeof(DistroActivityListener) },
            typeof(DistroActivityListener),
            skipVisibility: true);

        var il = method.GetILGenerator();
        var hasId = il.DefineLabel();

        il.Emit(OpCodes.Ldarg_0);                       // this
        il.Emit(OpCodes.Call, getValidatedId);          // string (id or null)
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brtrue_S, hasId);               // non-null id -> use it
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Call, createRandom);            // ActivityTraceId.CreateRandom()
        il.Emit(OpCodes.Ret);

        il.MarkLabel(hasId);
        il.Emit(OpCodes.Call, asSpan);                  // id.AsSpan() -> ReadOnlySpan<char>
        il.Emit(OpCodes.Call, createFromString);        // ActivityTraceId.CreateFromString(span)
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate(funcType, this);
    }

    // Never throws: returns the current transaction's NR trace id when it is 32 lower-hex chars and not all-zero (the
    // W3C trace-id shape CreateFromString requires), otherwise null so the caller falls back to a random id.
    private string GetValidatedTraceId()
    {
        try
        {
            var id = _sink.TryGetCurrentTraceId();
            if (id is { Length: 32 } && IsNonZeroLowerHex(id))
            {
                return id;
            }
        }
        catch
        {
            // Fall through to null -> random trace id.
        }

        return null;
    }

    // CreateFromString requires lower-case hex AND a non-zero value: it throws ArgumentOutOfRangeException on an
    // all-zero id, and the generator's IL calls it outside any try/catch, so an all-zero id must take the random path.
    private static bool IsNonZeroLowerHex(string value)
    {
        var nonZeroSeen = false;
        foreach (var c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
            {
                return false;
            }

            if (c != '0')
            {
                nonZeroSeen = true;
            }
        }

        return nonZeroSeen;
    }
}
