// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
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
    }

    // Same results as agent mode (ActivityBridge.ConfigureSampleCallback): AllDataAndRecorded or None.
    private ActivitySamplingResult Sample(ref ActivityCreationOptions<ActivityContext> options)
    {
        return _sink.ShouldSample((int)options.Kind, options.Parent.IsRemote)
            ? ActivitySamplingResult.AllDataAndRecorded
            : ActivitySamplingResult.None;
    }
}
