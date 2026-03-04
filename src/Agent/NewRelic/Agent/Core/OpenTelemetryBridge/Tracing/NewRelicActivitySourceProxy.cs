// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing.Interfaces;
using NewRelic.Agent.Extensions.Api.Experimental;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;

public class NewRelicActivitySourceProxy
{
    public const string SegmentCustomPropertyName = "NewRelicSegment";

    public const string ActivitySourceName = "NewRelic.Agent";

    private static INewRelicActivitySource _activitySource = null;
    private static int _usingRuntimeActivitySource = 0;

    public static void SetAndCreateRuntimeActivitySource(Type activitySourceType, Type activityKindType, IActivitySourceFactory factory = null)
    {
        // We only need to create the runtime activity source once. If it has already been created, we can return early.
        if (Interlocked.CompareExchange(ref _usingRuntimeActivitySource, 1, 0) == 1)
        {
            return;
        }

        var originalActivitySource = Interlocked.Exchange(ref _activitySource,
            new RuntimeActivitySource(ActivitySourceName, AgentInstallConfiguration.AgentVersion, activitySourceType, activityKindType, factory)) as IDisposable;
        originalActivitySource?.Dispose();
    }

    public INewRelicActivity TryCreateActivity(string activityName, ActivityKind kind) => _activitySource?.CreateActivity(activityName, kind);
}
