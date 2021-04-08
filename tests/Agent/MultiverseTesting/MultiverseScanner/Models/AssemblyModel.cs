// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace NewRelic.Agent.MultiverseScanner.Models
{
    public class AssemblyModel
    {
        ModuleDefinition _moduleDefinition;
        public string AssemblyName => _moduleDefinition.Assembly.Name.Name;
        public Dictionary<string, ClassModel> ClassModels { get; }

        public AssemblyModel(ModuleDefinition moduleDefinition)
        {
            _moduleDefinition = moduleDefinition;
            ClassModels = new Dictionary<string, ClassModel>();

            foreach (var typeDefinition in _moduleDefinition.Types)
            {
                if (!typeDefinition.IsClass || typeDefinition.FullName.StartsWith("<"))
                {
                    continue;
                }

                var classModel = new ClassModel(typeDefinition.FullName, GetAccessLevel(typeDefinition));
                BuildMethodModels(classModel, typeDefinition);
                ClassModels.Add(classModel.Name, classModel);
            }
        }

        private string GetAccessLevel(TypeDefinition typeDefinition)
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

        private void BuildMethodModels(ClassModel classModel, TypeDefinition typeDefinition)
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

    }
}
