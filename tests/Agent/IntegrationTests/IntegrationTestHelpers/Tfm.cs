// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTestHelpers;

/// <summary>
/// Central TFM constants for integration tests. Update these values when moving to a new .NET generation.
/// </summary>
public static class Tfm
{
    // On the .NET 11 preview eval branch, NetLatest is set to net11.0, and NetOldest is set to net10.0.
    public const string NetOldest = "net10.0";
    public const string NetLatest = "net11.0";

    /// <summary>Version-only forms used by container test DotnetVersion fields (no "net" prefix).</summary>
    public const string NetOldestVersion = "10.0";
    public const string NetLatestVersion = "11.0";

    // Newest supported LTS release. Smoke tests target only this version.
    public const string NetLtsVersion = "10.0";
}
