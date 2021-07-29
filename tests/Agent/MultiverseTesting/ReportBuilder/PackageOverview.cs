// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace ReportBuilder
{
    public class PackageOverview
    {
        public string PackageName { get; set; }

        /// <summary>
        /// Version, AssemblyOverview
        /// </summary>
        public Dictionary<string, AssemblyOverview> PackageVersions { get; set; }

        public PackageOverview(string packageName)
        {
            PackageName = packageName;
            PackageVersions = new Dictionary<string, AssemblyOverview>();
        }
    }
}
