// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace ReportBuilder
{
    public class InstrumentationOverview
    {
        /// <summary>
        /// InstrumentationSetName, List<PackageOverview>>
        /// </summary>
        public Dictionary<string, List<PackageOverview>> Reports { get; set; }

        public InstrumentationOverview()
        {
            Reports = new Dictionary<string, List<PackageOverview>>();
        }
    }
}
