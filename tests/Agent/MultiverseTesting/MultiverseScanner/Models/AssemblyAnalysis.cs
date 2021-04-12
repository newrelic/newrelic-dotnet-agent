// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.MultiverseScanner.Models
{
    public class AssemblyAnalysis
    {
        public AssemblyModel AssemblyModel { get; }

        public AssemblyAnalysis(AssemblyModel assemblyModel)
        {
            AssemblyModel = assemblyModel;
        }
        
        public int ClassesCount 
        { 
            get { return AssemblyModel.ClassModels.Count; } 
        }
    }
}
