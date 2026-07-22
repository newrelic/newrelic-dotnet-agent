// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Dotty;

public class NugetVersionData
{
    public string PackageName { get; set; }
    public string OldVersion { get; set; }
    public string NewVersion { get; set; }
    public Version NewVersionAsVersion { get; set; }
    public string Url { get; set; }
    public DateTime PublishDate { get; set; }
    public string IgnoreTfMs { get; }
    public Dictionary<string, string> TfmTargetVersions { get; }

    public NugetVersionData(string packageName, string oldVersion, string newVersion, string url,
        DateTime publishDate, string ignoreTfMs, Dictionary<string, string> tfmTargetVersions = null)
    {
        PackageName = packageName;
        OldVersion = oldVersion;
        NewVersion = newVersion;
        NewVersionAsVersion = new Version(newVersion);
        Url = url;
        PublishDate = publishDate;
        IgnoreTfMs = ignoreTfMs;
        TfmTargetVersions = tfmTargetVersions;
    }
}
