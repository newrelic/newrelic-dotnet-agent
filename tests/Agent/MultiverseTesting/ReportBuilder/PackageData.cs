// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace ReportBuilder
{
    public class PackageData
    {
        // TargetFramework, <method sig, isValid>
        public Dictionary<string, Dictionary<string, bool>> MethodSignatures { get; set; }  

        public PackageData(string targetFramework)
        {
            MethodSignatures = new Dictionary<string, Dictionary<string, bool>>();
        }
    }
}
