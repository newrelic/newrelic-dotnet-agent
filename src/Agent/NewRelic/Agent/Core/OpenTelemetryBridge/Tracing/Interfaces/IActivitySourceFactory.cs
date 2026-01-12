// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.OpenTelemetryBridge.Tracing.Interfaces
{
    public interface IActivitySourceFactory
    {
        object CreateActivitySource(string name, string version);
    }
}
