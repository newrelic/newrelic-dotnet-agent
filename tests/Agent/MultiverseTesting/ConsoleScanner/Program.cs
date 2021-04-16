// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NewRelic.Agent.MultiverseScanner;
using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;
using NewRelic.Agent.MultiverseScanner.Models;
using NewRelic.Agent.MultiverseScanner.Reporting;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NewRelic.Agent.ConsoleScanner
{
    public class Program
    {
        private static List<InstrumentationReport> _instrumentationReports = new List<InstrumentationReport>();
        private static XmlSerializer _xmlSerializer = new XmlSerializer(typeof(Extension));
        private static readonly string _nugetDataDirectory = $@"{Environment.CurrentDirectory}\NugetData";

        public static void Main(string[] args)
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                Console.WriteLine("ERROR Missing argument: Must supply path to configuration and report files.");
                return;
            }

            var configFilePath = args[0];
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("ERROR File not found: Provide path was incorrect or file missing.");
                return;
            }

            var reportFilePath = Path.GetFullPath(args[1]);
            if (File.Exists(reportFilePath))
            {
                Console.WriteLine($"Warning: Found existing report at '{reportFilePath}'.  It will be overwritten!");
            }

            var pathToReport = Directory.GetParent(reportFilePath).FullName;
            if (!Directory.Exists(pathToReport))
            {
                Console.WriteLine($"ERROR Directory not found: Provide path was incorrect or missing.");
                return;
            }

            // deserialize configuration from .yml
            var configuration = ScannerConfiguration.GetScannerConfiguration(configFilePath);

            ProcessAssemblies(configuration);
            var reports = SerializeReports();
            WriteReportToDisk(reports, reportFilePath);
            PrintReportToConsole();
         }

        public static void ProcessAssemblies(ScannerConfiguration configuration)
        {
            foreach (var instrumentationSet in configuration.InstrumentationSets)
            {
                // load the instrumentation.xml and create InstrumentationModel
                var instrumentationModel = ReadInstrumentationFile(instrumentationSet);

                // nuget assemblies
                var downloadedNugetInfoList = GetNugetAssemblies(instrumentationSet, instrumentationModel.UniqueAssemblies);

                foreach(var downloadedNugetInfo in downloadedNugetInfoList)
                {
                    foreach (var intrumentedDllFileLocation in downloadedNugetInfo.InstrumentedDllFileLocations)
                    {
                        // Builds a model from the files
                        Console.WriteLine($"Starting scan of '{intrumentedDllFileLocation}'");
                        var assemblyAnalyzer = new AssemblyAnalyzer();
                        var assemblyAnalysis = assemblyAnalyzer.RunAssemblyAnalysis(intrumentedDllFileLocation);

                        var instrumentationValidator = new InstrumentationValidator(assemblyAnalysis);

                        // just some debugging writes
                        Console.WriteLine($"Found {assemblyAnalysis.ClassesCount} classes");
                        Console.WriteLine("Scan complete");

                        var targetFramework = Path.GetFileName(Path.GetDirectoryName(intrumentedDllFileLocation));

                        // run the validation
                        var report = instrumentationValidator.CheckInstrumentation(instrumentationModel, instrumentationSet.Name, targetFramework, downloadedNugetInfo.PackageVersion, downloadedNugetInfo.PackageName);
                        _instrumentationReports.Add(report);
                    }
                }
            }
        }

        public static InstrumentationModel ReadInstrumentationFile(InstrumentationSet instrumentationSet)
        {
            Extension extension = null;

            using (var fileStream = File.Open(instrumentationSet.XmlFile, FileMode.Open))
            {
                extension = (Extension)_xmlSerializer.Deserialize(fileStream);
            }

            var instrumentationModel = InstrumentationModel.CreateInstrumentationModel(extension);
            return instrumentationModel;
        }

        public static List<DownloadedNugetInfo> GetNugetAssemblies(InstrumentationSet instrumentationSet, List<string> instrumentationAssemblies)
        {
            var downloadedNugetInfoList = new List<DownloadedNugetInfo>();
            if (instrumentationSet.NugetPackages != null)
            {
                foreach (var nugetPackage in instrumentationSet.NugetPackages)
                {
                    downloadedNugetInfoList.AddRange(GetNugetPackages(nugetPackage.PackageName, nugetPackage.Versions, instrumentationAssemblies));

                }
            }

            return downloadedNugetInfoList;
        }

        public static List<DownloadedNugetInfo> GetNugetPackages(string packageName, List<string> versions, List<string> instrumentationAssemblies)
        {
            var downloadedNugetInfos = new List<DownloadedNugetInfo>();

            try
            {
                Directory.CreateDirectory(_nugetDataDirectory);
                var client = new NugetClient(_nugetDataDirectory);
                versions.Add(client.GetLatestVersion(packageName)); // add current version to versions list
                foreach (var version in versions.Distinct()) // using Distinct to prevent duplicates
                {
                    var dllFileLocations = new List<string>();
                    var nugetExtractDirectoryName = client.DownloadPackage(packageName, version);

                    // TODO: this will get every version (net45, netstandard1.5) of the dll in the package, which may not be necessary; 
                    foreach (var instrumentationAssembly in instrumentationAssemblies)
                    {
                        dllFileLocations.AddRange(Directory.GetFiles(nugetExtractDirectoryName, "*.dll", SearchOption.AllDirectories));
                    }

                    downloadedNugetInfos.Add(new DownloadedNugetInfo(dllFileLocations, version, packageName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetNugetPackages exception : Package - {packageName}");
                Console.WriteLine($"GetNugetPackages exception : {ex}");
            }

            return downloadedNugetInfos;
        }

        public static void PrintReportToConsole()
        {
            Console.WriteLine("============ REPORT ============");
            foreach(var report in _instrumentationReports)
            {
                Console.WriteLine($"Instrumentation set: {report.InstrumentationSetName}");
                Console.WriteLine($"Nuget package: {report.PackageName} ver {report.PackageVersion}");
                Console.WriteLine($"Target framework: {report.TargetFramework}");
                Console.WriteLine($"");

                foreach (var assemblyReport in report.AssemblyReports)
                {
                    Console.Write("\t");
                    Console.WriteLine($"Assembly Name: {assemblyReport.AssemblyName}");

                    var methodValidations = assemblyReport.GetMethodValidationsForPrint();
                    foreach (var line in methodValidations)
                    {
                        Console.WriteLine($"\t{line}");
                    }
                }

                Console.WriteLine($"");
            }
        }

        public static string SerializeReports()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            return serializer.Serialize(_instrumentationReports);
        }

        public static void WriteReportToDisk(string reports, string reportFilePath)
        {
            File.WriteAllText(reportFilePath, reports);
        }
    }
}
