// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTestHelpers;

/// <summary>
/// Turns a runtime fact into a platform requirement. The Linux and Windows-Core
/// lanes select by excluding this trait, so absence means portable.
/// </summary>
public static class PlatformTrait
{
    public const string TraitName = "Platform";
    public const string WindowsOnlyValue = "WindowsOnly";
    public const string PortableLabel = "Portable";
    public const string UnknownLabel = "Unknown";

    public static bool RequiresWindows(RuntimeLane lane)
    {
        return lane == RuntimeLane.Framework;
    }

    public static string ToRequirementLabel(RuntimeLane lane)
    {
        if (lane == RuntimeLane.Unknown)
        {
            return UnknownLabel;
        }

        return RequiresWindows(lane) ? WindowsOnlyValue : PortableLabel;
    }
}
