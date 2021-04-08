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
                // TODO: deal with duplicate assembly names

                // load the instrumentation.xml and create InstrumentationModel
                var instrumentationModel = ReadInstrumentationFile(instrumentationSet);

                List<string> dllFileLocations = new List<string>();

                // nuget assemblies
                var nugetDllFileLocations = GetNugetAssemblies(instrumentationSet, instrumentationModel.UniqueAssemblies);
                if (nugetDllFileLocations != null)
                {
                    dllFileLocations.AddRange(nugetDllFileLocations);
                }

                // local assemblies
                var localDllFileLocations = GetLocalAssemblies(instrumentationSet);
                if (localDllFileLocations != null)
                {
                    dllFileLocations.AddRange(localDllFileLocations);
                }
                var dllFileNames = dllFileLocations.ToArray();

                // Builds a model from the files
                Console.WriteLine($"Starting scan of '{string.Join(',', dllFileNames)}'");
                var assemblyAnalyzer = new AssemblyAnalyzer();
                var assemblyAnalysis = assemblyAnalyzer.RunAssemblyAnalysis(dllFileNames);

                var instrumentationValidator = new InstrumentationValidator(assemblyAnalysis);

                // just some debugging writes
                Console.WriteLine($"Found {assemblyAnalysis.ClassesCount} classes");
                Console.WriteLine("Scan complete");

                // run the validation
                var report = instrumentationValidator.CheckInstrumentation(instrumentationModel, instrumentationSet.Name);
                _instrumentationReports.Add(report);
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

        // TODO: not the best name, considering all it does
        public static List<string> GetNugetPackages(string packageName, string[] versions, List<string> instrumentationAssemblies)
        {
            var addressPrefix = $"{_nugetSource}/{packageName.ToLower()}";
            List<string> dllFileLocations = new List<string>();
            try
            {
                var webClient = new WebClient();


                Directory.CreateDirectory(_nugetDataDirectory);

                foreach (var version in versions)
                {
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

                        // extract dlls from package
                        
                        if (Directory.Exists(nugetExtractDirectoryName))
                        {
                            Directory.Delete(nugetExtractDirectoryName, true);
                        }
                        Directory.CreateDirectory(nugetExtractDirectoryName);
                        ZipFile.ExtractToDirectory(nugetDownloadedPackageFileName, nugetExtractDirectoryName);
                    }

                    // TODO: this will get every version (net45, netstandard1.5) of the dll in the package, which may not be necessary; 
                    foreach (var instrumentationAssembly in instrumentationAssemblies)
                    {
                        dllFileLocations.AddRange(Directory.GetFiles(nugetExtractDirectoryName, instrumentationAssembly + ".dll", SearchOption.AllDirectories));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetNugetPackages exception : Package - {addressPrefix}");
                Console.WriteLine($"GetNugetPackages exception : {ex}");
            }

            return dllFileLocations;
        }

        public static List<string> GetNugetAssemblies(InstrumentationSet instrumentationSet, List<string> instrumentationAssemblies)
        {
            List<string> fileList = new List<string>();
            if (instrumentationSet.NugetPackages != null)
            {
                foreach (var nugetPackage in instrumentationSet.NugetPackages)
                {
                    fileList.AddRange(GetNugetPackages(nugetPackage.PackageName, nugetPackage.Versions, instrumentationAssemblies));
                }
            }
            return fileList;
        }

        public static List<string> GetLocalAssemblies(InstrumentationSet instrumentationSet)
        {
            return instrumentationSet.LocalAssemblies;
        }

        public static void PrintReport()
        {
            Console.WriteLine("============ REPORT ============");
            foreach(var report in _instrumentationReports)
            {
                Console.WriteLine($"Instrumentation Set: {report.InstrumentationSetName}");
                foreach(var assemblyReport in report.AssemblyReports)
                {
                    Console.Write("\t");
                    Console.WriteLine($"Assembly Name: {assemblyReport.AssemblyName}; Assembly Version: {assemblyReport.AssemblyVersion}");

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
