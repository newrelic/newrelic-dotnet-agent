// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Mono.Cecil;
using NewRelic.Agent.MultiverseScanner.Models;

namespace NewRelic.Agent.MultiverseScanner
{
    // https://github.com/jbevain/cecil/wiki/HOWTO

    public class AssemblyAnalyzer
    {
        public AssemblyAnalysis RunAssemblyAnalysis(string filePath)
        {
            var assemblyModel = GetAssemblyModel(filePath);

            var assemblyAnalysis = new AssemblyAnalysis(assemblyModel);

            return assemblyAnalysis;
        }

        public AssemblyModel GetAssemblyModel(string filePath)
        {
            var moduleDefinition = ModuleDefinition.ReadModule(filePath);
            var assemblyModel = new AssemblyModel(moduleDefinition);
            return assemblyModel;
        }
    }
}
