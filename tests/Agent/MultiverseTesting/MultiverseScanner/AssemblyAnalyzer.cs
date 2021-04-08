// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Mono.Cecil;
using NewRelic.Agent.MultiverseScanner.Models;

namespace NewRelic.Agent.MultiverseScanner
{
    // https://github.com/jbevain/cecil/wiki/HOWTO

    public class AssemblyAnalyzer
    {
        public AssemblyAnalysis RunAssemblyAnalysis(params string[] filePaths)
        {
            var assemblyAnalysis = new AssemblyAnalysis();

            foreach (var filePath in filePaths)
            {
                var assemblyModel = GetAssemblyModel(filePath);

                // TODO: need to allow duplicates for multiple versions
                // for now, don't add it if it's in there
                // refactor - Dictionary may not be the right data structure 
                if (assemblyAnalysis.AssemblyModels.ContainsKey(assemblyModel.AssemblyName))
                {
                    continue;
                }
                assemblyAnalysis.AssemblyModels.Add(assemblyModel.AssemblyName, assemblyModel);
            }

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
