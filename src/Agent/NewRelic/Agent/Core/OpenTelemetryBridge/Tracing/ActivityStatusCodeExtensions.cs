// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;

public static class ActivityStatusCodeExtensions
{
    public static string ToActivityStatusCodeString(this int statusCode)
    {
        return EnumNameCache<ActivityStatusCode>.GetNameToLower((ActivityStatusCode)statusCode);
    }
}
