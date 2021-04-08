// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;
using NewRelic.Agent.MultiverseScanner.Models;
using NewRelic.Agent.MultiverseScanner.Reporting;

namespace NewRelic.Agent.MultiverseScanner
{
    public class InstrumentationValidator
    {
        private AssemblyAnalysis _assemblyAnalysis;

        public InstrumentationValidator(AssemblyAnalysis assemblyAnalysis)
        {
            _assemblyAnalysis = assemblyAnalysis;
        }

        public InstrumentationReport CheckInstrumentation(InstrumentationModel instrumentationModel, string instrumentationSetName, string targetFramework)
        {
            var instrumentationReport = new InstrumentationReport()
            {
                InstrumentationSetName = instrumentationSetName,
                TargetFramework = targetFramework
            };


            // Check each AssemblyModel against all instrumentation
            // InstrumentationReport will show aggregated results from all assemblies
            foreach (var assemblyModel in _assemblyAnalysis.AssemblyModels.Values)
            {
                var assemblyReport = new AssemblyReport();

                assemblyReport.AssemblyName = assemblyModel.AssemblyName;

                CheckMatch(assemblyModel, instrumentationModel, assemblyReport);

                instrumentationReport.AssemblyReports.Add(assemblyReport);
            }

            return instrumentationReport;
        }

        public void CheckMatch(AssemblyModel assemblyModel, InstrumentationModel instrumentationModel, AssemblyReport instrumentationReport)
        {
            foreach (var match in instrumentationModel.Matches)
            {
                if(!ValidateAssembly(assemblyModel, match, instrumentationReport))
                {
                    continue;
                }

                // assembly match checking classes and methods
                ValidateClass(assemblyModel, match, instrumentationReport);
            }
        }

        public bool ValidateAssembly(AssemblyModel assemblyModel, Match match, AssemblyReport instrumentationReport)
        {
            // okay to move on to checking classes
            if (match.AssemblyName == assemblyModel.AssemblyName)
            {
                return true;
            }

            // assembly did not match so marking all methods as false - can be changed by later validation attempts
            // TODO: don't want invalid on methods that aren't in the instrumented assembly, right?
            //MarkAllMethodsAsNotValid(match, instrumentationReport);
            return false;
        }

        public void ValidateClass(AssemblyModel assemblyModel, Match match, AssemblyReport instrumentationReport)
        {
            // check if class exists in ClassModels and get ClassModel back
            if (assemblyModel.ClassModels.TryGetValue(match.ClassName, out var classModel))
            {
                CheckExactMethodMatchers(instrumentationReport, match, classModel);
                return;
            }

            foreach (var exactMethodMatcher in match.ExactMethodMatchers)
            {
                instrumentationReport.AddMethodValidation(match, exactMethodMatcher, false);
            }
            // class did not match so marking all methods as false - can be changed by later validation attempts
            //MarkAllMethodsAsNotValid(match, instrumentationReport);
        }

        public void CheckExactMethodMatchers(AssemblyReport instrumentationReport, Match match, ClassModel classModel)
        {
            foreach (var exactMethodMatcher in match.ExactMethodMatchers)
            {
                // check if method exists in MethodModels and get MethodModel back
                if (classModel.MethodModels.TryGetValue(exactMethodMatcher.MethodName, out var methodModel))
                {
                    // Check if exactMethodMatcher.Parameters is empty or popluated
                    if (string.IsNullOrWhiteSpace(exactMethodMatcher.Parameters))
                    {
                        // exactMethodMatcher has NO params, checking of MethodModel has an empty ParameterSets value
                        //if (methodModel.ParameterSets.Contains(string.Empty))
                        //{
                        //    instrumentationReport.AddMethodValidation(match, exactMethodMatcher, true);
                        //    continue;
                        //}

                        instrumentationReport.AddMethodValidation(match, exactMethodMatcher, true);
                        continue;
                    }

                    // exactMethodMatcher HAS params to check
                    if (methodModel.ParameterSets.Contains(exactMethodMatcher.Parameters))
                    {
                        instrumentationReport.AddMethodValidation(match, exactMethodMatcher, true);
                        continue;
                    }

                    // param was not found
                    instrumentationReport.AddMethodValidation(match, exactMethodMatcher, false);
                    continue;
                }
                else
                {
                    // Did not find method in classmodel, amrking method as false
                    instrumentationReport.AddMethodValidation(match, exactMethodMatcher, false);
                }
            }
        }

        public void MarkAllMethodsAsNotValid(Match match, AssemblyReport instrumentationReport)
        {

            // Did not match so marking all methods as false - can be changed by later validation attempts
            foreach (var exactMethodMatcher in match.ExactMethodMatchers)
            {
                instrumentationReport.AddMethodValidation(match, exactMethodMatcher, false);
            }
        }

    }
}
