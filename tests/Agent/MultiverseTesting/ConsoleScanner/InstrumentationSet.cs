// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace NewRelic.Agent.ConsoleScanner
{
    public class InstrumentationSet
    {
        public string Name { get; set; }

        public string XmlFile { get; set; }

        public List<NugetSet> NugetAssemblies { get; set; }

        public List<string> LocalAssemblies { get; set; }

        public class NugetSet
        {
            public string AssemblyName { get; set; }
            public string[] Versions { get; set; }

            public string[] GetDllFileLocations(string path)
            {
                List<string> dllFileLocations = new List<string>();
                return dllFileLocations.ToArray();
            }
        }
    }
}
