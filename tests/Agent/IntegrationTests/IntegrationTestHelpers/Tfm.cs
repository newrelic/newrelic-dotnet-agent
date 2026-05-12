// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTestHelpers;

/// <summary>
/// Central TFM constants for integration tests. Update these two values when moving to a new .NET generation.
/// </summary>
public static class Tfm
{
    public const string NetOldest = "net10.0";
    public const string NetLatest = "net11.0";

    /// <summary>Version-only forms used by container test DotnetVersion fields (no "net" prefix).</summary>
    public const string NetOldestVersion = "10.0";
    public const string NetLatestVersion = "11.0";
}
