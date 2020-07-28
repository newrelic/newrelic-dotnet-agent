using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using MoreLinq;

namespace NewRelic.Installer
{
    public class Program
    {
        private const string HomeDirectoryNamePrefix = "src\\Agent\\New Relic Home ";
        private const string ProfilerSoFileName = "libNewRelicProfiler.so";

        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Global

        [CommandLine.Option("solution", Required = true, HelpText = "$(SolutionDir)")]
        public string SolutionPath { get; set; }

        [CommandLine.Option("configuration", Required = false, HelpText = "$(Configuration)")]
        public string Configuration { get; set; }

        [CommandLine.Option("nugetPackageDir", Required = false, HelpText = "$(NuGetPackageRoot)")]
        public string NuGetPackageDir { get; set; }

        private bool _isCoreClr = false;
        private bool _isLinux = false;

        public string Bitness { get; set; }

        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global

        // output paths
        private string DestinationHomeDirectoryName
        {
            get
            {
                var name = HomeDirectoryNamePrefix + Bitness;
                if (_isCoreClr)
                {
                    name += " CoreClr";
                }
                if (_isLinux)
                {
                    name += "_Linux";
                }

                return name;
            }
        }

        private string DestinationHomeDirectoryPath { get { return Path.Combine(SolutionPath, DestinationHomeDirectoryName); } }
        private string DestinationAgentFilePath { get { return Path.Combine(DestinationHomeDirectoryPath, "NewRelic.Agent.Core.dll"); } }
        private string DestinationProfilerDllPath => Path.Combine(DestinationHomeDirectoryPath, "NewRelic.Profiler.dll");
        private string DestinationProfilerSoPath => Path.Combine(DestinationHomeDirectoryPath, ProfilerSoFileName);
        private string DestinationExtensionsDirectoryPath { get { return Path.Combine(DestinationHomeDirectoryPath, "Extensions"); } }
        private string DestinationRegistryFileName { get { return string.Format("src\\Agent\\New Relic Home {0}.reg", Bitness); } }
        private string DestinationRegistryFilePath { get { return Path.Combine(SolutionPath, DestinationRegistryFileName); } }
        private string DestinationNewRelicConfigXsdPath { get { return Path.Combine(DestinationHomeDirectoryPath, "newrelic.xsd"); } }
        private string BuildOutputPath { get { return Path.Combine(SolutionPath, "src", "_build"); } }
        private string AnyCpuBuildPath { get { return Path.Combine(BuildOutputPath, AnyCpuBuildDirectoryName); } }

        // input paths
        private string AnyCpuBuildDirectoryName { get { return string.Format("AnyCPU-{0}", Configuration); } }
        private string NewRelicConfigPath { get { return Path.Combine(SolutionPath, "src", "Agent", "Configuration", "newrelic.config") ?? string.Empty; } }
        private string NewRelicConfigXsdPath { get { return Path.Combine(SolutionPath, "src", "Agent", "NewRelic", "Agent", "Core", "Config", "Configuration.xsd"); } }
        private string ExtensionsXsdPath { get { return Path.Combine(SolutionPath, "src", "Agent", "NewRelic", "Agent", "Core", "NewRelic.Agent.Core.Extension", "extension.xsd"); } }
        private string NewRelicAgentCoreCsprojPath { get { return Path.Combine(SolutionPath, "src", "Agent", "NewRelic", "CoreInstaller"); } }
        private string LicenseSourceDirectoryPath { get { return Path.GetFullPath(Path.Combine(SolutionPath, "licenses")); } }
        private string LicenseFilePath => Path.Combine(LicenseSourceDirectoryPath, "LICENSE.txt");
        private string ThirdPartyNoticesFilePath => Path.Combine(LicenseSourceDirectoryPath, "THIRD_PARTY_NOTICES.txt");
        private readonly string Core20ReadmeFileName = "netcore20-agent-readme.md";
        private string ReadmeFilePath => Path.Combine(SolutionPath, "src", "Agent", "Miscellaneous", Core20ReadmeFileName);
        private string AgentApiPath => Path.Combine(AnyCpuBuildPath, "NewRelic.Api.Agent", _isCoreClr ? "netstandard2.0" : "net35", "NewRelic.Api.Agent.dll");
        private string AgentVersion => FileVersionInfo.GetVersionInfo(DestinationAgentFilePath).FileVersion;

        private string ProfilerDllPath
        {
            get
            {
                var profilerPath = Path.Combine(SolutionPath, "src", "Agent", "ProfilerBuildsForDevMachines", "Windows", Bitness, "NewRelic.Profiler.dll");
                return profilerPath;
            }
        }

        private string ProfilerSoPath
        {
            get
            {
                var folderPath = Path.Combine(SolutionPath, "src", "Agent", "ProfilerBuildsForDevMachines", "Linux", "libNewRelicProfiler.so");
                var profilerSoPath = Path.Combine(folderPath, ProfilerSoFileName);
                return profilerSoPath;
            }
        }

        private string AgentCoreBuildDirectoryPath { get { return Path.Combine(AnyCpuBuildPath, @"NewRelic.Agent.Core", _isCoreClr ? "netstandard2.0" : "net35"); } }
        private string ILRepackedNewRelicAgentCorePath { get { return Path.Combine(AgentCoreBuildDirectoryPath + "-ILRepacked", "NewRelic.Agent.Core.dll"); } }
        private string NewRelicCoreBuildDirectoryPath { get { return Path.Combine(AnyCpuBuildPath, @"NewRelic.Core", _isCoreClr ? "netstandard2.0" : "net35"); } }
        private string NewRelicCorePath { get { return Path.Combine(NewRelicCoreBuildDirectoryPath, "NewRelic.Core.dll"); } }
        private string NewRelicAgentExtensionsPath { get { return Path.Combine(AgentCoreBuildDirectoryPath, "NewRelic.Agent.Extensions.dll"); } }
        private string ExtensionsDirectoryPath { get { return Path.Combine(SolutionPath, "src", "Agent", "NewRelic", "Agent", "Extensions"); } }
        private string KeyFilePath { get { return Path.Combine(SolutionPath, "build", "keys", "NewRelicStrongNameKey.snk"); } }

        void RealMain()
        {
            DoWork(bitness: "x86", isCoreClr: false);
            DoWork(bitness: "x64", isCoreClr: false);
            DoWork(bitness: "x86", isCoreClr: true);
            DoWork(bitness: "x64", isCoreClr: true);
            DoWork(bitness: "x64", isCoreClr: true, isLinux: true);
        }

        private void DoWork(string bitness, bool isCoreClr, bool isLinux = false)
        {
            Console.WriteLine($"AnyCpuBuildDirectoryName {AnyCpuBuildDirectoryName}");

            Bitness = bitness;
            _isCoreClr = isCoreClr;
            _isLinux = isLinux;

            var frameworkMsg = _isCoreClr ? "CoreCLR" : ".NETFramework";
            frameworkMsg += _isLinux ? " Linux" : "";
            Console.WriteLine($"[HomeBuilder]: Building home for {frameworkMsg} {bitness}");
            Console.WriteLine("[HomeBuilder]: attempting to read and restore CustomInstrumentation.xml");

            var customInstrumentationFilePath = Path.Combine(DestinationExtensionsDirectoryPath, "CustomInstrumentation.xml");
            byte[] customInstrumentationBytes = ReadCustomInstrumentationBytes(customInstrumentationFilePath);

            ReCreateDirectoryWithEveryoneAccess(DestinationHomeDirectoryPath);

            Directory.CreateDirectory(DestinationExtensionsDirectoryPath);

            CopyProfiler(isLinux);

            File.Copy(NewRelicConfigXsdPath, DestinationNewRelicConfigXsdPath, true);
            CopyToDirectory(ILRepackedNewRelicAgentCorePath, DestinationHomeDirectoryPath);
            CopyToDirectory(NewRelicConfigPath, DestinationHomeDirectoryPath);
            CopyToDirectory(ExtensionsXsdPath, DestinationExtensionsDirectoryPath);
            CopyToDirectory(NewRelicAgentExtensionsPath, DestinationHomeDirectoryPath);
            CopyAgentExtensions();
            CopyOtherDependencies();

            CopyToDirectory(NewRelicCorePath, DestinationExtensionsDirectoryPath);

            var shouldCreateRegistryFile = (isCoreClr == false);
            if (shouldCreateRegistryFile)
            {
                CreateRegistryFile();
            }

            if (customInstrumentationBytes != null)
            {
                File.WriteAllBytes(customInstrumentationFilePath, customInstrumentationBytes);
            }
        }

        private void CopyProfiler(bool isLinux = false)
        {
            if (isLinux)
            {
                var soExists = File.Exists(ProfilerSoPath);
                if (soExists)
                {
                    Console.WriteLine($"[HomeBuilder]: Copying Linux profiler Shared Object (so) from: {ProfilerSoPath} to: {DestinationProfilerSoPath}");
                    File.Copy(ProfilerSoPath, DestinationProfilerSoPath, true);
                }
                else
                {
                    Console.WriteLine($"[HomeBuilder]: *** Did not find Linux profiler Shared Object (so) at path: {ProfilerSoPath} ***");
                }
            }
            else
            {
                Console.WriteLine($"[HomeBuilder]: Copying Windows profiler DLL from: {ProfilerDllPath} to: {DestinationProfilerDllPath}");
                File.Copy(ProfilerDllPath, DestinationProfilerDllPath, true);
            }
        }

        private byte[] ReadCustomInstrumentationBytes(string customInstrumentationFilePath)
        {
            byte[] customInstrumentationBytes = null;
            if (File.Exists(customInstrumentationFilePath))
            {
                customInstrumentationBytes = File.ReadAllBytes(customInstrumentationFilePath);
            }

            return customInstrumentationBytes;
        }

        private void CopyOtherDependencies()
        {
            CopyToDirectory(LicenseFilePath, DestinationHomeDirectoryPath);
            CopyToDirectory(ThirdPartyNoticesFilePath, DestinationHomeDirectoryPath);

            if (_isCoreClr)
            {
                CopyToDirectory(AgentApiPath, DestinationHomeDirectoryPath);
                CopyToDirectory(ReadmeFilePath, DestinationHomeDirectoryPath);
                File.Move(Path.Combine(DestinationHomeDirectoryPath, Core20ReadmeFileName), Path.Combine(DestinationHomeDirectoryPath, "README.md"));
                return;
            }

            // We copy JetBrains Annotations to the output extension folder because many of the extensions use it. Even though it does not need to be there for the extensions to work, sometimes our customers will use frameworks that do assembly scanning (such as EpiServer) that will panic when references are unresolved.
            var jetBrainsAnnotationsAssemblyPath = Path.Combine(AgentCoreBuildDirectoryPath, "JetBrains.Annotations.dll");
            CopyToDirectory(jetBrainsAnnotationsAssemblyPath, DestinationExtensionsDirectoryPath);
        }

        private static void ReCreateDirectoryWithEveryoneAccess(string directoryPath)
        {
            try
            { Directory.Delete(directoryPath, true); }
            catch (DirectoryNotFoundException) { }

            Thread.Sleep(TimeSpan.FromMilliseconds(1));

            var directoryInfo = Directory.CreateDirectory(directoryPath);
            var directorySecurity = directoryInfo.GetAccessControl();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            directorySecurity.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directoryInfo.SetAccessControl(directorySecurity);
        }

        private static void CopyToDirectory(string sourceFilePath, string destinationDirectoryPath)
        {
            if (sourceFilePath == null)
                throw new ArgumentNullException("sourceFilePath");

            if (destinationDirectoryPath == null)
                throw new ArgumentNullException("destinationDirectoryPath");

            var fileName = Path.GetFileName(sourceFilePath);
            var destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);
            File.Copy(sourceFilePath, destinationFilePath, true);
        }

        private void CopyAgentExtensions()
        {
            var directoriesWithoutFramework = Directory.EnumerateDirectories(ExtensionsDirectoryPath, Configuration, SearchOption.AllDirectories);
            List<string> allDirectoriesForConfiguration = new List<string>(directoriesWithoutFramework);
            foreach (var directory in directoriesWithoutFramework)
            {
                var frameworkSubDirectories = Directory.EnumerateDirectories(directory, "*net*");
                allDirectoriesForConfiguration.AddRange(frameworkSubDirectories);
            }

            var netstandardProjectsToIncludeInBothAgents = new[] { "AspNetCore" };
            var directories = allDirectoriesForConfiguration.ToList()
                .Where(directoryPath => directoryPath != null)
                .Where(directoryPath => directoryPath.Contains("netstandard") == _isCoreClr)
                .Select(directoryPath => new DirectoryInfo(directoryPath))
                .Where(directoryInfo => directoryInfo.Parent != null)
                .Where(directoryInfo => directoryInfo.Parent.Name == "bin" || directoryInfo.Parent.Name == Configuration)
                .ToList();

            var dlls = directories
                .SelectMany(directoryInfo => directoryInfo.EnumerateFiles("*.dll"))
                .Where(fileInfo => fileInfo != null)
                .DistinctBy(fileInfo => fileInfo.Name)
                .Where(fileInfo => fileInfo != null)
                .Select(fileInfo => fileInfo.FullName)
                .Where(filePath => filePath != null)
                .Where(filePath => FileVersionInfo.GetVersionInfo(filePath).FileVersion == AgentVersion)
                .Distinct()
                .ToList();

            foreach (var filePath in dlls)
            {
                var destination = DestinationExtensionsDirectoryPath;
                CopyNewRelicAssemblies(filePath, destination);
                TryCopyExtensionInstrumentationFile(filePath, DestinationExtensionsDirectoryPath);
            };
        }

        private static void CopyNewRelicAssemblies(string assemblyFilePath, string destinationExtensionsDirectoryPath)
        {
            var directoryPath = Path.GetDirectoryName(assemblyFilePath);
            if (directoryPath == null)
                return;

            var directoryInfo = new DirectoryInfo(directoryPath);
            var filePaths = directoryInfo
                .EnumerateFiles("NewRelic.*.dll")
                .Where(fileInfo => fileInfo != null)
                .Where(fileInfo => fileInfo.Name != "NewRelic.Agent.Extensions.dll")
                .Where(fileInfo => !fileInfo.Name.EndsWith("Tests.dll"))
                .Select(fileInfo => fileInfo.FullName)
                .Where(filePath => filePath != null)
                .ToList();

            foreach (var filePath in filePaths)
            {
                CopyToDirectory(filePath, destinationExtensionsDirectoryPath);
            }
        }

        private static void TryCopyExtensionInstrumentationFile(string assemblyFilePath, string destinationExtensionsDirectoryPath)
        {
            var directory = Path.GetDirectoryName(assemblyFilePath);
            if (directory == null)
                return;

            var instrumentationFilePath = Path.Combine(directory, "Instrumentation.xml");
            if (!File.Exists(instrumentationFilePath))
                return;

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);
            if (assemblyName == null)
                return;

            var destinationFilePath = Path.Combine(destinationExtensionsDirectoryPath, assemblyName + ".Instrumentation.xml");
            File.Copy(instrumentationFilePath, destinationFilePath, true);
        }

        private void CreateRegistryFile()
        {
            var strings = new[]
            {
                @"COR_ENABLE_PROFILING=1",
                @"COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}",
                string.Format(@"COR_PROFILER_PATH={0}", DestinationProfilerDllPath),
                string.Format(@"NEWRELIC_HOME={0}\", DestinationHomeDirectoryPath)
            };

            var bytes = new List<byte>();
            foreach (var @string in strings)
            {
                if (@string == null)
                    continue;
                bytes.AddRange(Encoding.Unicode.GetBytes(@string));
                bytes.AddRange(new byte[] { 0, 0 });
            }
            bytes.AddRange(new byte[] { 0, 0 });

            var hexString = BitConverter.ToString(bytes.ToArray()).Replace('-', ',');
            const string fileContentsFormatter =
@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\W3SVC]
""Environment""=hex(7):{0}

[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WAS]
""Environment""=hex(7):{0}
";
            var fileContents = string.Format(fileContentsFormatter, hexString);
            File.WriteAllText(DestinationRegistryFilePath, fileContents);
        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.ParseCommandLineArguments(args);
            program.RealMain();
        }

        private void ParseCommandLineArguments(string[] commandLineArguments)
        {
            var defaultParser = CommandLine.Parser.Default;
            if (defaultParser == null)
                throw new NullReferenceException("defaultParser");

            defaultParser.ParseArgumentsStrict(commandLineArguments, this);
        }

        private string GetNuGetPackageFolder(string csprojPath, string packageName)
        {

            Console.WriteLine($"Searching local Package Cache for '{packageName}' in {NuGetPackageDir}");
            var version = GetNuGetPackageVersion(csprojPath, packageName);
            var pkgFolder = Path.Combine(NuGetPackageDir.TrimEnd('"'), packageName, version);
            Console.WriteLine($"Nuget Package Folder - {pkgFolder}");
            return pkgFolder;
        }

        private string GetNuGetPackageFolderNativeLibPath(string csprojPath, string packageName)
        {
            var pkgFolder = GetNuGetPackageFolder(csprojPath, packageName);
            pkgFolder = Path.Combine(pkgFolder, "runtimes");
            pkgFolder = _isLinux
                ? Path.Combine(pkgFolder, "linux")
                : Path.Combine(pkgFolder, "win");

            pkgFolder = Path.Combine(pkgFolder, "native");
            Console.WriteLine($"Nuget Package LibPath - {pkgFolder}");
            return pkgFolder;
        }

        private string GetNuGetPackageVersion(string csprojPath, string packageName)
        {
            var regex = new Regex($@".*<PackageReference Include=""{packageName}"" Version=""(.*?)"" />");
            var packageVersion = File.ReadAllLines(csprojPath)
                .Select(line => regex.Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value)
                .FirstOrDefault();

            Console.WriteLine($"Identified Package Reference to '{packageName}' for version {packageVersion} in {csprojPath}");
            return packageVersion;
        }
    }
}
