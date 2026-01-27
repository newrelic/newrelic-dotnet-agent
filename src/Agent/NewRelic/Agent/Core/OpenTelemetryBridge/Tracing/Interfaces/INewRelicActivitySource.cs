// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using NewRelic.Agent.Extensions.Api.Experimental;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing.Interfaces;

public interface INewRelicActivitySource : IDisposable
{
    INewRelicActivity CreateActivity(string activityName, ActivityKind kind);
}