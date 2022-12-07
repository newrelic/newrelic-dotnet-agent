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
        // This list does not include some specific frameworks as explained below
        // .NET Framework 3/4.x.x: these could be the only version in older packages and will still work in apps targeting up to 4.8.x.
        // .NET Standard: all versions prior to 2.0 support .NET Framework 4.5+ so we they might be used on their own.
        // Microsoft Store (Windows Store) "netcore" is listed directly in the ShouldScan method since adding it here would block other allowed frameworks.
        /// <summary>
        /// This is a list of prefixes to be ignored.  It is meant to be used with string.StartsWith()."
        /// </summary>
        private static List<string> _excludedFrameworks = new List<string> {
            "net1", // .NET Framework 1.x.x
            "net2", // .NET Framework 2.x.x
            "netcoreapp1", // .NET Core 1.x.x (this version of .NET was completely different from 2.x.x)
            "netcore45", // Microsoft Store (Windows Store) - Windows 8.x
            "netcore5", // Microsoft Store (Windows Store)
            "netmf", // .NET MicroFramework
        };

        private static bool _writeLogs = true;

        private static List<InstrumentationReport> _instrumentationReports = new List<InstrumentationReport>();
        private static XmlSerializer _xmlSerializer = new XmlSerializer(typeof(Extension));
        private static readonly string _nugetDataDirectory = $@"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}NugetData";

        public static void Main(string[] args)
        {
            if (args.Length != 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                WriteLineToConsole("ERROR Missing argument: Must supply path to configuration and report files.");
                return;
            }

            var configFilePath = args[0];
            if (!File.Exists(configFilePath))
            {
                WriteLineToConsole("ERROR File not found: Provide path was incorrect or file missing.");
                return;
            }

            var reportFilePath = Path.GetFullPath(args[1]);
            if (File.Exists(reportFilePath))
            {
                WriteLineToConsole($"Warning: Found existing report at '{reportFilePath}'.  It will be overwritten!");
            }

            var pathToReport = Directory.GetParent(reportFilePath).FullName;
            if (!Directory.Exists(pathToReport))
            {
                WriteLineToConsole($"ERROR Directory not found: Provide path was incorrect or missing.");
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
                        // Checkl for and skip unusable frameworks like silverlight
                        var targetFramework = Path.GetFileName(Path.GetDirectoryName(intrumentedDllFileLocation));
                        if (!ShouldScan(targetFramework))
                        {
                            continue;
                        }

                        // Builds a model from the files
                        WriteLineToConsole($"Starting scan of '{intrumentedDllFileLocation}'");
                        var assemblyAnalyzer = new AssemblyAnalyzer();
                        var assemblyAnalysis = assemblyAnalyzer.RunAssemblyAnalysis(intrumentedDllFileLocation);

                        var instrumentationValidator = new InstrumentationValidator(assemblyAnalysis);

                        // just some debugging writes
                        WriteLineToConsole($"Found {assemblyAnalysis.ClassesCount} classes");
                        WriteLineToConsole("Scan complete");

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
                    downloadedNugetInfoList.AddRange(GetNugetPackages(nugetPackage.PackageName, nugetPackage.Versions, instrumentationAssemblies, instrumentationSet.DownloadLatest));
                }
            }

            return downloadedNugetInfoList;
        }

        public static List<DownloadedNugetInfo> GetNugetPackages(string packageName, List<string> versions, List<string> instrumentationAssemblies, bool downloadLatest)
        {
            var downloadedNugetInfos = new List<DownloadedNugetInfo>();

            try
            {
                Directory.CreateDirectory(_nugetDataDirectory);
                var client = new NugetClient(_nugetDataDirectory);
                if (downloadLatest)
                {
                    versions.Add(client.GetLatestVersion(packageName)); // add current version to versions list
                }
                else
                {
                    Console.WriteLine($"Not downloading latest version of package {packageName} based on config.");
                }
                    
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
                WriteLineToConsole($"GetNugetPackages exception : Package - {packageName}");
                WriteLineToConsole($"GetNugetPackages exception : {ex}");
            }

            return downloadedNugetInfos;
        }

        public static void PrintReportToConsole()
        {
            WriteLineToConsole("============ REPORT ============");
            foreach(var report in _instrumentationReports)
            {
                WriteLineToConsole($"Instrumentation set: {report.InstrumentationSetName}");
                WriteLineToConsole($"Nuget package: {report.PackageName} ver {report.PackageVersion}");
                WriteLineToConsole($"Target framework: {report.TargetFramework}");
                WriteLineToConsole($"");
                WriteToConsole("\t");
                WriteLineToConsole($"Assembly Name: {report.AssemblyReport.AssemblyName}");
                var methodValidations = report.AssemblyReport.GetMethodValidationsForPrint();
                foreach (var line in methodValidations)
                {
                    WriteLineToConsole($"\t{line}");
                }
                

                WriteLineToConsole($"");
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

        private static void WriteToConsole(string entry)
        {
            if (_writeLogs)
            {
                Console.Write(entry);
            }
        }

        private static void WriteLineToConsole(string entry)
        {
            if (_writeLogs)
            {
                Console.WriteLine(entry);
            }
        }

        private static bool ShouldScan(string targetFramework)
        {
            if (!targetFramework.StartsWith("net") || targetFramework == "netcore") // "netcore" is a Windows Store framework, but shares a prefix with .NET Core.
            {
                return false;
            }

            foreach (var excludedFramework in _excludedFrameworks)
            {
                if (targetFramework.StartsWith(excludedFramework))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
