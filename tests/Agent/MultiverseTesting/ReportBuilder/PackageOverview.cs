// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace ReportBuilder
{
    public class PackageOverview
    {
        public string PackageName { get; set; }

        /// <summary>
        /// Version, new Dictionary<method sig, isValid>()
        /// </summary>
        //public Dictionary<string, Dictionary<string, bool>> PackageVersions { get; set; }

        public Dictionary<string, PackageData> Versions { get; set; }

        public PackageOverview(string packageName)
        {
            PackageName = packageName;
            Versions = new Dictionary<string, PackageData>();
        }
    }
}
