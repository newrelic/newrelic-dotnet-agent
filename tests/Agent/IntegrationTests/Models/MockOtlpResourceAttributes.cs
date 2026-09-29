// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.IntegrationTests.Models;

public static class MockOtlpResourceAttributes
{
    public static readonly IReadOnlyDictionary<string, string> Values = new Dictionary<string, string>
    {
        { "tags.team", "dotnet-agent" },
        { "host", "mock-collector-host" },
        { "k8s.podName", "" },
        { "licenseKey", "12345678" }
    };
}
