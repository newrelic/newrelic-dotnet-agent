// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace ReportBuilder
{
    public class AssemblyOverview
    {
        public string AssemblyName { get; set; }

        /// <summary>
        /// Fully Qualified Method Signature, Is Valid
        /// </summary>
        public Dictionary<string, bool> MethodSignatures { get; set; }

        public AssemblyOverview(string assemblyName)
        {
            AssemblyName = assemblyName;
            MethodSignatures = new Dictionary<string, bool>();
        }
    }
}
