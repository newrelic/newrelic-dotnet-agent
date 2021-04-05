// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.MultiverseScanner.Models
{
    public class AssemblyAnalysis
    {
        public Dictionary<string, AssemblyModel> AssemblyModels { get; }

        public AssemblyAnalysis()
        {
            AssemblyModels = new Dictionary<string, AssemblyModel>();
        }


        // TODO: is this needed for anything except logging to output?
        public int ClassesCount 
        { 
            get { return AssemblyModels.Select((x) => x.Value.ClassModels.Count).ToList().Sum(); } 
        }
    }
}
