// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.IntegrationTests.Models;

public static class MockOtlpResourceAttributes
{
    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        { "realAgentId", "1147483647" },
        { "host", "mock-agent-host" },
        { "host.displayName", "mock-agent-display-host" },
        { "instanceName", "mock-agent-instance" },
        { "agent.version", "10.99.0.0" },
        { "appName", "MockAppName" },
        { "licenseKey", "b25fd3ca" }
    };
}
