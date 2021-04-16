// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.MultiverseScanner.Reporting
{
    public class AssemblyReport
    {
        public readonly Dictionary<string, List<MethodValidation>> Validations;

        public string AssemblyName { get; set; }

        public AssemblyReport()
        {
            Validations = new Dictionary<string, List<MethodValidation>>();
        }

        public List<string> GetMethodValidationsForPrint()
        {
            List<string> text = new List<string>();
            foreach (KeyValuePair<string, List<MethodValidation>> kvp in Validations)
            {
                text.Add($"Class Name: {kvp.Key}");
                foreach (var mv in kvp.Value)
                {
                    var isFoundInDll = mv.IsValid ? "is" : "is NOT";
                    text.Add($"\tMethod: {mv.MethodSignature} {isFoundInDll} instrumented.");
                }
            }

            return text;
        }

        public void AddMethodValidation(Match match, ExactMethodMatcher exactMethodMatcher, bool isValid)
        {
            // check if a class item has already been added and return it
            if (Validations.TryGetValue(match.ClassName, out var methodValidations))
            {
                // attempt to get an existing MethodValidation so we can update it.
                var methodValidation = methodValidations.FirstOrDefault((x) => x.MethodSignature == exactMethodMatcher.MethodSignature);
                if (methodValidation == null)
                {
                    // No existing MethodValidation
                    methodValidations.Add(new MethodValidation(exactMethodMatcher, isValid));
                }
                else
                {
                    // found an existing MethodValidation
                    // Only allow changes from false to true
                    if (isValid)
                    {
                        methodValidation.IsValid = isValid;
                    }
                }
            }

            // did not find class
            if (!Validations.ContainsKey(match.ClassName))
            {
                Validations.Add(match.ClassName, new List<MethodValidation>());
                Validations[match.ClassName].Add(new MethodValidation(exactMethodMatcher, isValid));
            }

        }
    }
}
