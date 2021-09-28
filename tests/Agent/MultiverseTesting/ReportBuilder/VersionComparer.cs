// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;

namespace ReportBuilder
{
    class VersionComparer : IComparer<DirectoryInfo>
    {
        // This will report things in reverse order -- newest to oldet.
        public int Compare(DirectoryInfo x, DirectoryInfo y)
        {
            var versionX = new Version(x.Name.Trim('v'));
            var versionY = new Version(y.Name.Trim('v'));
            return versionY.CompareTo(versionX);
        }
    }
}
