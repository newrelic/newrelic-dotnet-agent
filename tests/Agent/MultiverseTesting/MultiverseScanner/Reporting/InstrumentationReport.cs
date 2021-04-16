// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace NewRelic.Agent.MultiverseScanner.Reporting
{
    public class InstrumentationReport
    {
        public string InstrumentationSetName;

        public string PackageVersion;

        public string TargetFramework;

        public string PackageName;

        public List<AssemblyReport> AssemblyReports = new List<AssemblyReport>();
    }
}
