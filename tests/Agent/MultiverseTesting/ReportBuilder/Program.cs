// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            var reportFilePath = args[1];
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
            //var wikiMaker = new WikiMaker();
            //wikiMaker.UpdateWiki(outputPath, agentVersion, overview);
            var htmlMaker = new HtmlMaker();
            htmlMaker.UpdatePages(outputPath, agentVersion, overview);
        }

        private static InstrumentationOverview TransformReport(List<InstrumentationReport> instrumentationReports)
        {
            var overview = new InstrumentationOverview();
            foreach (var instrumentationReport in instrumentationReports)
            {
                // look for existing set in reports, create new with new PO list if needed
                if (!overview.Reports.ContainsKey(instrumentationReport.InstrumentationSetName))
                {
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
                if (!packageOverview.PackageVersions.ContainsKey(instrumentationReport.PackageVersion))
                {
                    packageOverview.PackageVersions.Add(instrumentationReport.PackageVersion, new Dictionary<string, bool>());
                }

                var methodSignatures = packageOverview.PackageVersions[instrumentationReport.PackageVersion];
                foreach (var method in instrumentationReport.AssemblyReport.Validations)
                {
                    foreach (var validation in method.Value)
                    {
                        var fqMethodname = $"{method.Key}.{validation.MethodSignature}";
                        if (methodSignatures.TryGetValue(fqMethodname, out var isValid) )
                        {
                            if(isValid == validation.IsValid)
                            {
                                continue;
                            }

                            throw new Exception($"ERROR: Method '{fqMethodname}' exists with different validation result. Has {isValid} should be {validation.IsValid}");
                        }

                        methodSignatures.Add(fqMethodname, validation.IsValid);
                    }
                }
            }

            return overview;
        }
	}
}
