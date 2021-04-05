// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Mono.Cecil;
using NewRelic.Agent.MultiverseScanner.Models;
using System;
using System.Linq;

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
            var assemblyModel = new AssemblyModel(moduleDefinition.Assembly.Name.Name, GetAssemblyVersion(moduleDefinition));
            BuildClassModels(assemblyModel, moduleDefinition);
            return assemblyModel;
        }

        public void BuildClassModels(AssemblyModel assemblyModel, ModuleDefinition moduleDefinition)
        {
            foreach (var typeDefinition in moduleDefinition.Types)
            {
                if (!typeDefinition.IsClass || typeDefinition.FullName.StartsWith("<"))
                {
                    continue;
                }

                var classModel = new ClassModel(typeDefinition.FullName, GetAccessLevel(typeDefinition));
                BuildMethodModels(classModel, typeDefinition);
                assemblyModel.AddClass(classModel);
            }
        }

        public void BuildMethodModels(ClassModel classModel, TypeDefinition typeDefinition)
        {
            foreach (var method in typeDefinition.Methods)
            {
                var methodModel = classModel.GetOrCreateMethodModel(method.Name);
                if (method.HasParameters)
                {
                    var parameters = method.Parameters.Select((x) => x.ParameterType.FullName.Replace('<', '[').Replace('>', ']')).ToList();
                    methodModel.ParameterSets.Add(string.Join(",", parameters));
                }
                else
                {
                    // covers a method having no parameters.
                    methodModel.ParameterSets.Add(string.Empty);
                }
            }
        }

        public string GetAccessLevel(TypeDefinition typeDefinition)
        {
            if (typeDefinition.IsPublic)
            {
                return "public";
            }
            else if (typeDefinition.IsNotPublic)
            {
                return "private";
            }

            
            return "";
        }

        public Version GetAssemblyVersion(ModuleDefinition moduleDefinition)
        {
            var assemblyName = new System.Reflection.AssemblyName(moduleDefinition.Assembly.FullName);
            return assemblyName.Version;
        }
    }
}
