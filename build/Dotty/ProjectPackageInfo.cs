// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace Dotty;

public class ProjectPackageInfo
{
    public string PackageName { get; set; }
    public string PackageVersion { get; set; }
    public string Tfm { get; set; }
}

public static class VersionHelpers
{
    public static Version AsVersion(this string version)
    {
        return new Version(version);
    }

    public static bool IsMajorBump(string oldVersion, string newVersion)
    {
        return Version.TryParse(oldVersion, out var oldParsed)
            && Version.TryParse(newVersion, out var newParsed)
            && newParsed.Major > oldParsed.Major;
    }
}