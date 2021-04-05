// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.MultiverseScanner.Reporting
{
    public class InstrumentationReport
    {
        public string InstrumentationSetName;
        public List<AssemblyReport> AssemblyReports = new List<AssemblyReport>();
    }

    public class AssemblyReport
    {
        private Dictionary<string, List<MethodValidation>> _validations;

        public string AssemblyName { get; set; }

        public Version AssemblyVersion { get; set; }

        public AssemblyReport()
        {
            _validations = new Dictionary<string, List<MethodValidation>>();
        }

        public List<string> GetMethodValidationsForPrint()
        {
            List<string> text = new List<string>();
            foreach (KeyValuePair<string, List<MethodValidation>> kvp in _validations)
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
            if (_validations.TryGetValue(match.ClassName, out var methodValidations))
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
            if (!_validations.ContainsKey(match.ClassName))
            {
                _validations.Add(match.ClassName, new List<MethodValidation>());
                _validations[match.ClassName].Add(new MethodValidation(exactMethodMatcher, isValid));
            }

        }
    }
}
