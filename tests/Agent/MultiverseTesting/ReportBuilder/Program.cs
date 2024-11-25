// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.MultiverseScanner.Reporting;

namespace ReportBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.WriteLine("ERROR Missing argument: Must supply agent version, path to report, and an output path.");
                return;
            }

            var agentVersion = args[0];
            if (string.IsNullOrWhiteSpace(agentVersion))
            {
                Console.WriteLine("ERROR Agent version was missing: Provide The agent version in X.X.X format.");
                return;
            }

            var reportFilePath = Path.GetFullPath(args[1]);
            if (!File.Exists(reportFilePath))
            {
                Console.WriteLine("ERROR File not found: Provide path was incorrect or file missing.");
                return;
            }

            var outputPath = Path.GetFullPath(args[2]);
            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine($"ERROR Directory not found: Provide path was incorrect or directory missing");
            }

            // deserialize reports from .yml
            var reports = ReportParser.GetInstrumentationReports(reportFilePath);
            var overview = TransformReport(reports);
            var htmlMaker = new HtmlMaker();
            htmlMaker.UpdatePages(outputPath, agentVersion, overview);
            htmlMaker.SaveRawReport(reportFilePath, outputPath, agentVersion);
        }

        private static InstrumentationOverview TransformReport(List<InstrumentationReport> instrumentationReports)
        {
            var overview = new InstrumentationOverview();
            foreach (var instrumentationReport in instrumentationReports)
            {
                if (!instrumentationReport.TargetFramework.StartsWith("net") || instrumentationReport.TargetFramework == "net4" || instrumentationReport.TargetFramework == "net4-client")
                {
                    continue;
                }

                if (!overview.Reports.ContainsKey(instrumentationReport.InstrumentationSetName))
                {
                    // look for existing set in reports, create new with new PO list if needed
                    overview.Reports.Add(instrumentationReport.InstrumentationSetName, new List<PackageOverview>());
                }

                // get the PO list
                var packageOverviewList = overview.Reports[instrumentationReport.InstrumentationSetName];

                // get existing packageOverview from list or create a new one and add to packageOverviewList
                var packageOverview = packageOverviewList.FirstOrDefault(po => po.PackageName == instrumentationReport.PackageName);
                if (packageOverview == null)
                {
                    packageOverview = new PackageOverview(instrumentationReport.PackageName);
                    packageOverviewList.Add(packageOverview);
                }

                // add package version if does not exist
                if (!packageOverview.Versions.ContainsKey(instrumentationReport.PackageVersion))
                {
                    packageOverview.Versions.Add(instrumentationReport.PackageVersion, new PackageData(instrumentationReport.TargetFramework));
                }

                var packageData = packageOverview.Versions[instrumentationReport.PackageVersion];
                if (!packageData.MethodSignatures.ContainsKey(instrumentationReport.TargetFramework))
                {
                    packageData.MethodSignatures.Add(instrumentationReport.TargetFramework, new Dictionary<string, bool>());
                }

                var methodSignatures = packageData.MethodSignatures[instrumentationReport.TargetFramework];
                foreach (var method in instrumentationReport.AssemblyReport.Validations)
                {
                    foreach (var validation in method.Value)
                    {
                        var fqMethodname = $"{method.Key}.{validation.MethodSignature}";
                        if (methodSignatures.TryGetValue(fqMethodname, out var isValid) )
                        {
                            // checks if the instrumentation changes from one targetFramework to another
                            if(isValid == validation.IsValid)
                            {
                                continue;
                            }

                            throw new Exception($"ERROR: Method '{fqMethodname}' in '{instrumentationReport.PackageName}':'{instrumentationReport.PackageVersion}' exists with different validation result for .NET version '{instrumentationReport.TargetFramework}'. Has {isValid} should be {validation.IsValid}");
                        }

                        methodSignatures[fqMethodname] = validation.IsValid;
                    }
                }
            }

            return overview;
        }
    }
}
