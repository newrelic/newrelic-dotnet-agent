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
            ProcessOverview(outputPath, agentVersion, overview);


            /*
             * assumes we are in the mvs folder
             * check if version folder exists, rename if does
             * create new version folder
             * create framework files and populate, store name
             * create home file using stored names
             * Scan mvs folder and recreate mvs home file
             * 
             */
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
                    packageOverview.PackageVersions.Add(instrumentationReport.PackageVersion, new AssemblyOverview(instrumentationReport.AssemblyReport.AssemblyName));
                }

                var assemblyOverview = packageOverview.PackageVersions[instrumentationReport.PackageVersion];
                foreach (var method in instrumentationReport.AssemblyReport.Validations)
                {
                    foreach (var validation in method.Value)
                    {
                        var fqMethodname = $"{method.Key}.{validation.MethodSignature}";
                        if (assemblyOverview.MethodSignatures.TryGetValue(fqMethodname, out var isValid) )
                        {
                            if(isValid == validation.IsValid)
                            {
                                continue;
                            }

                            throw new Exception($"ERROR: Method '{fqMethodname}' exists with different validation result. Has {isValid} should be {validation.IsValid}");
                        }

                        assemblyOverview.MethodSignatures.Add(fqMethodname, validation.IsValid);
                    }
                }
            }

            return overview;
        }





        private static void ProcessOverview(string outputPath, string version, InstrumentationOverview instrumentationOverview)
        {
            var dirs = SetupDirectoryStructure(outputPath, version);
            SetupDirectoryContents(dirs, instrumentationOverview);
        }

        private static DirectoryInfo SetupDirectoryStructure(string outputPath, string version)
        {
            var fullPath = Path.GetFullPath(version, outputPath);
            if (Directory.Exists(fullPath))
            {
                Directory.Move(fullPath, fullPath + "_old");
            }

            return Directory.CreateDirectory(fullPath);
        }

        private static void SetupDirectoryContents(DirectoryInfo directoryInfo, InstrumentationOverview instrumentationOverview)
        {
            foreach (var report in instrumentationOverview.Reports)
            {
                CreateFrameworkFile(directoryInfo, report);
            }


        }

        private static void CreateFrameworkFile(DirectoryInfo directoryInfo, KeyValuePair<string, List<PackageOverview>> report)
        {
            var filePath = CreateFile(report.Key, directoryInfo.FullName);

            var builder = new StringBuilder();
            builder.AppendLine(string.Empty);

            foreach (var packageOverview in report.Value)
            {
                // Package_Name B
                builder.AppendLine($"{packageOverview.PackageName}");
                builder.AppendLine(string.Empty);

                builder.Append("| Method/Version ");
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    // | Method/Version | 1.0 | 2.0 | 3.0 | 4.0 |
                    builder.Append($"| {versionedAssemblyOverview.Key} ");
                }

                builder.AppendLine("|");

                // |---|---|---|---|---|
                builder.Append("|---");
                for (var i = 0; i < packageOverview.PackageVersions.Count; i++)
                {
                    builder.Append("|---");
                }

                builder.AppendLine("|");

                // | assembly.class.method() |   |   |   |   |
                foreach (var versionedAssemblyOverview in packageOverview.PackageVersions)
                {
                    var methodSignatures = versionedAssemblyOverview.Value.MethodSignatures;
                    foreach (var assemblyOverview in methodSignatures)
                    {
                        builder.Append("| ");
                        // write out method sig
                        builder.Append(assemblyOverview.Key);
                    }


                    /* Ran into an issue
                     * Need to write out all the isValids for a methods sig at each version (as a row), basically, assembly then version
                     * Right now the Overview is structures to iterate over version and then assembly (as a column)
                     */
                    
                }
            }






            // versions header
            // header border
            // sigs

            // set name

            //using (var file = File.)
            //{

            //}
            var lame = builder.ToString();
        }

        private static void CreateVersionHomeFile()
        {

        }

        private static string CreateFile(string path, string basePath)
        {
            var filePath = Path.GetFullPath(path, basePath);
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
            }

            return filePath;
        }
	}
}
