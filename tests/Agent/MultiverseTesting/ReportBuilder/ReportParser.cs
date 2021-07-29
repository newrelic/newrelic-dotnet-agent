// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using NewRelic.Agent.MultiverseScanner.Reporting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ReportBuilder
{
    public class ReportParser
    {
        public static List<InstrumentationReport> GetInstrumentationReports(string filePath)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var data = deserializer.Deserialize(File.OpenText(filePath)) as List<object>;
            return BuildReports(data);
        }

        private static List<InstrumentationReport> BuildReports(List<object> reports)
        {
            var instrumentationReports = new List<InstrumentationReport>();
            foreach (Dictionary<object, object> report in reports)
            {
                instrumentationReports.Add(ProcessReport(report));
            }

            return instrumentationReports;
        }

        private static InstrumentationReport ProcessReport(Dictionary<object, object> report)
        {
            var instrumentationReport = new InstrumentationReport();
            foreach (KeyValuePair<object, object> item in report)
            {
                var key = item.Key as string;

                if (key.Equals("instrumentation_set_name"))
                {
                    instrumentationReport.InstrumentationSetName = item.Value as string;
                }
                else if (key.Equals("package_version"))
                {
                    instrumentationReport.PackageVersion = item.Value as string;
                }
                else if (key.Equals("target_framework"))
                {
                    instrumentationReport.TargetFramework = item.Value as string;
                }
                else if (key.Equals("package_name"))
                {
                    instrumentationReport.PackageName = item.Value as string;
                }
                else if (key.Equals("assembly_report"))
                {
                    instrumentationReport.AssemblyReport = ProcessAssemblyReport(item.Value as Dictionary<object, object>);
                }
            }

            return instrumentationReport;
        }

        private static AssemblyReport ProcessAssemblyReport(Dictionary<object, object> assemblyReport)
        {
            var assemblyReportInt = new AssemblyReport();
            foreach (KeyValuePair<object, object> item in assemblyReport)
            {
                var key = item.Key as string;
                if (key.Equals("assembly_name"))
                {
                    assemblyReportInt.AssemblyName = item.Value as string;
                }
                else if (key.Equals("validations"))
                {
                    assemblyReportInt.Validations = ProcessValidation(item.Value as Dictionary<object, object>);
                }
            }

            return assemblyReportInt;
        }

        private static Dictionary<string, List<MethodValidation>> ProcessValidation(Dictionary<object, object> validations)
        {
            var validationDictionary = new Dictionary<string, List<MethodValidation>>();
            foreach (var validation in validations)
            {
                var className = validation.Key as string;
                var methodValidationList = new List<MethodValidation>();
                var methodValidations = validation.Value as List<object>;
                foreach (Dictionary<object, object> methodValidation in methodValidations)
                {
                    var methodSig = methodValidation["method_signature"] as string;
                    var isValid = methodValidation["is_valid"] as string;
                    methodValidationList.Add(new MethodValidation(methodSig, Convert.ToBoolean(isValid)));
                }

                validationDictionary.Add(className, methodValidationList);
            }

            return validationDictionary;
        }
    }
}
