// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTestHelpers;

/// <summary>
/// Central TFM constants for integration tests. Update these two values when moving to a new .NET generation.
/// </summary>
public static class Tfm
{
    // On the .NET 11 preview eval branch, NetLatest is set to net11.0, and NetOldest is set to net10.0.
    // We still need .NET 8 for the Azure Functions in-process model tests, so I have added that as a separate constant.
    // TBD what to do with the in-process model tests; that model is being deprecated in November 2026 at the same time
    // .NET 11 is released, so we may want to stop testing it at all.  
    public const string Net8 = "net8.0";
    public const string NetOldest = "net10.0";
    public const string NetLatest = "net11.0";

    /// <summary>Version-only forms used by container test DotnetVersion fields (no "net" prefix).</summary>
    public const string NetOldestVersion = "10.0";
    public const string NetLatestVersion = "11.0";
}
