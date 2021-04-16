// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace NewRelic.Agent.ConsoleScanner
{
    public class DownloadedNugetInfo
    {
        public DownloadedNugetInfo(List<string> dllFileLocations, string version, string packageName)
        {
            InstrumentedDllFileLocations = dllFileLocations;
            PackageVersion = version;
            PackageName = packageName;
        }

        public List<string> InstrumentedDllFileLocations { get; }

        public string PackageVersion { get; }

        public string PackageName { get; }
    }
}
