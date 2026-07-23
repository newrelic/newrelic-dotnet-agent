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
}