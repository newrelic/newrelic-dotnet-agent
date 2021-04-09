// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Xml.Serialization;
using NewRelic.Agent.MultiverseScanner;
using NewRelic.Agent.MultiverseScanner.ExtensionSerialization;
using NewRelic.Agent.MultiverseScanner.Models;
using NewRelic.Agent.MultiverseScanner.Reporting;

namespace NewRelic.Agent.ConsoleScanner
{
    public class Program
    {
        private static List<InstrumentationReport> _instrumentationReports = new List<InstrumentationReport>();
        private static XmlSerializer _xmlSerializer = new XmlSerializer(typeof(Extension));
        private const string _nugetDataDirectory = "NugetData";
        private const string _nugetSource = "https://api.nuget.org/v3-flatcontainer";

        public static void Main(string[] args)
        {
            if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine("ERROR Missing argument: Must supply path to configuration file.");
                return;
            }

            var configFilePath = args[0];
            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("ERROR File not found: Provide path was incorrect or file missing.");
                return;
            }

            // deserialize configuration from .yml
            var configuration = ScannerConfiguration.GetScannerConfiguration(configFilePath);

            ProcessAssemblies(configuration);

            PrintReport();
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

                        // run the validation
                        var report = instrumentationValidator.CheckInstrumentation(instrumentationModel, instrumentationSet.Name, instrumentationSet.TargetFramework, downloadedNugetInfo.PackageVersion, downloadedNugetInfo.PackageName);
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

        public static List<DownloadedNugetInfo> GetNugetPackages(string packageName, string targetFramework, string[] versions, List<string> instrumentationAssemblies)
        {
            var addressPrefix = $"{_nugetSource}/{packageName.ToLower()}";

            var downloadedNugetInfos = new List<DownloadedNugetInfo>();

            try
            {
                var webClient = new WebClient();


                Directory.CreateDirectory(_nugetDataDirectory);

                foreach (var version in versions)
                {
                    var dllFileLocations = new List<string>();

                    // set up
                    var nugetDownloadedPackagePrefix = $"{packageName.ToLower()}.{version.ToLower()}";
                    var nugetExtractDirectoryName = $"{_nugetDataDirectory}\\{nugetDownloadedPackagePrefix}";
                    var nugetDownloadedPackageFileName = $"{_nugetDataDirectory}\\{nugetDownloadedPackagePrefix}.zip";

                    // example address: https://api.nuget.org/v3-flatcontainer/mongodb.driver.core/2.6.0/mongodb.driver.core.2.6.0.nupkg
                    var address = $"{addressPrefix}/{version.ToLower()}/{packageName.ToLower()}.{version.ToLower()}.nupkg";

                    // skip downloading on re-run 
                    if (!File.Exists(nugetDownloadedPackageFileName))
                    {
                        // download nuget package
                        var result = webClient.DownloadData(address);
                        File.WriteAllBytes(nugetDownloadedPackageFileName, result);

                        Console.WriteLine($"Downloaded package {packageName} {version}");
                    }

                    // extract dlls from package

                    if (Directory.Exists(nugetExtractDirectoryName))
                    {
                        Directory.Delete(nugetExtractDirectoryName, true);
                    }
                    Directory.CreateDirectory(nugetExtractDirectoryName);
                    ZipFile.ExtractToDirectory(nugetDownloadedPackageFileName, nugetExtractDirectoryName);

                    // TODO: this will get every version (net45, netstandard1.5) of the dll in the package, which may not be necessary; 
                    foreach (var instrumentationAssembly in instrumentationAssemblies)
                    {
                        dllFileLocations.AddRange(Directory.GetFiles(nugetExtractDirectoryName, instrumentationAssembly + ".dll", SearchOption.AllDirectories));
                    }

                    dllFileLocations.RemoveAll(dll => !dll.Contains(targetFramework));

                    downloadedNugetInfos.Add(new DownloadedNugetInfo(dllFileLocations, version, packageName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetNugetPackages exception : Package - {addressPrefix}");
                Console.WriteLine($"GetNugetPackages exception : {ex}");
            }

            return downloadedNugetInfos;
        }

        public static List<DownloadedNugetInfo> GetNugetAssemblies(InstrumentationSet instrumentationSet, List<string> instrumentationAssemblies)
        {
            var downloadedNugetInfoList = new List<DownloadedNugetInfo>();
            if (instrumentationSet.NugetPackages != null)
            {
                foreach (var nugetPackage in instrumentationSet.NugetPackages)
                {
                    downloadedNugetInfoList.AddRange(GetNugetPackages(nugetPackage.PackageName, instrumentationSet.TargetFramework, nugetPackage.Versions, instrumentationAssemblies));
                }
            }
            return downloadedNugetInfoList;
        }

        public static void PrintReport()
        {
            Console.WriteLine("============ REPORT ============");
            foreach(var report in _instrumentationReports)
            {
                Console.WriteLine($"Instrumentation Set: {report.InstrumentationSetName}");
                Console.WriteLine($"Target Framework: {report.TargetFramework}");
                Console.WriteLine($"Nuget Package Version: {report.PackageVersion}");
                Console.WriteLine($"Nuget Package Name: {report.PackageName}");

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
            }
        }
    }
}
