// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.MultiverseScanner.Models
{
    public class AssemblyModel
    {
        public string AssemblyName { get; }

        public Version AssemblyVersion { get; }

        public Dictionary<string, ClassModel> ClassModels { get; }

        public AssemblyModel(string assemblyName, Version assemblyVersion)
        {
            AssemblyName = assemblyName;
            AssemblyVersion = assemblyVersion;
            ClassModels = new Dictionary<string, ClassModel>();
        }

        public void AddClass(ClassModel classModel)
        {
            ClassModels.Add(classModel.Name, classModel);
        }
    }
}
