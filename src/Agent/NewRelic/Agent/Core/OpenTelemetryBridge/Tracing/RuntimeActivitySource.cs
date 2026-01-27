// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq.Expressions;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing.Interfaces;
using NewRelic.Agent.Extensions.Api.Experimental;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;

public class RuntimeActivitySource : INewRelicActivitySource
{
    private readonly dynamic _activitySource;
    private readonly Func<string, int, object> _createActivityMethod;

    public RuntimeActivitySource(string name, string version, Type activitySourceType, Type activityKindType, IActivitySourceFactory factory = null)
    {
        if (factory != null)
        {
            _activitySource = factory.CreateActivitySource(name, version);
        }
        else
        {
            _activitySource = Activator.CreateInstance(activitySourceType, name, version);
        }
        _createActivityMethod = CreateCreateActivityMethod(activitySourceType, activityKindType);
    }

    public void Dispose()
    {
        _activitySource?.Dispose();
    }

    public INewRelicActivity CreateActivity(string activityName, ActivityKind kind)
    {
        var activity = _createActivityMethod(activityName, (int)kind);
        return new RuntimeNewRelicActivity(activity);
    }

    private Func<string, int, object> CreateCreateActivityMethod(Type activitySourceType, Type activityKindType)
    {
        var activityNameParameter = Expression.Parameter(typeof(string), "activityName");
        var activityKindParameter = Expression.Parameter(typeof(int), "kind");

        var typedActivityKind = Expression.Convert(activityKindParameter, activityKindType);
        var activitySourceInstance = Expression.Constant(_activitySource, activitySourceType);

        var startActivityMethod = activitySourceType.GetMethod("CreateActivity", [typeof(string), activityKindType]);
        var startActivityCall = Expression.Call(activitySourceInstance, startActivityMethod, activityNameParameter, typedActivityKind);
        var startActivityLambda = Expression.Lambda<Func<string, int, object>>(startActivityCall, activityNameParameter, activityKindParameter);
        return startActivityLambda.Compile();
    }
}
