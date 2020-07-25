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
using ILRepacking;
using MoreLinq;

namespace NewRelic.Installer
{
    public class Program
    {
        enum VersionResolution { Latest, Earliest, FirstFound, LastFound };

        private const string HomeDirectoryNamePrefix = "New Relic Home ";
        private const string ProfilerSoFileName = "libNewRelicProfiler.so";

        [CommandLine.Option("solution", Required = true, HelpText = "$(SolutionDir)")]
        public String SolutionPath { get; set; }

        [CommandLine.Option("configuration", Required = false, HelpText = "$(Configuration)")]
        public String Configuration { get; set; }

        [CommandLine.Option("nugetPackageDir", Required = false, HelpText = "$(NuGetPackageRoot)")]
        public String NuGetPackageDir { get; set; }

        private bool _isCoreClr = false;
        private bool _isLinux = false;
        public String Bitness { get; set; }

        // output paths
        private String DestinationHomeDirectoryName
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
        private String DestinationHomeDirectoryPath { get { return Path.Combine(SolutionPath, DestinationHomeDirectoryName); } }
        private String DestinationAgentFilePath { get { return Path.Combine(DestinationHomeDirectoryPath, "NewRelic.Agent.Core.dll"); } }
        private string DestinationProfilerDllPath => Path.Combine(DestinationHomeDirectoryPath, "NewRelic.Profiler.dll");
        private string DestinationProfilerSoPath => Path.Combine(DestinationHomeDirectoryPath, ProfilerSoFileName);
        private String DestinationExtensionsDirectoryPath { get { return Path.Combine(DestinationHomeDirectoryPath, "Extensions"); } }
        private String DestinationRegistryFileName { get { return String.Format("New Relic Home {0}.reg", Bitness); } }
        private String DestinationRegistryFilePath { get { return Path.Combine(SolutionPath, DestinationRegistryFileName); } }
        private String DestinationNewRelicConfigXsdPath { get { return Path.Combine(DestinationHomeDirectoryPath, "newrelic.xsd"); } }
        private String BuildOutputPath { get { return Path.Combine(SolutionPath, "_build"); } }
        private String AnyCpuBuildPath { get { return Path.Combine(BuildOutputPath, AnyCpuBuildDirectoryName); } }
        private String CoreInstallerOutputPath { get { return Path.Combine(BuildOutputPath, "core_installer"); } }

        // input paths
        private String AnyCpuBuildDirectoryName { get { return String.Format("AnyCPU-{0}", Configuration); } }
        private String NewRelicConfigPath { get { return Path.Combine(SolutionPath, "Configuration", "newrelic.config") ?? String.Empty; } }
        private String NewRelicConfigXsdPath { get { return Path.Combine(SolutionPath, "NewRelic", "Agent", "Core", "Config", "Configuration.xsd"); } }
        private String ExtensionsXsdPath { get { return Path.Combine(SolutionPath, "NewRelic", "Agent", "Core", "NewRelic.Agent.Core.Extension", "extension.xsd"); } }
        private String CoreInstallerSourcePath { get { return Path.Combine(SolutionPath, "NewRelic", "CoreInstaller"); } }

        private string LicenseFilePath => Path.Combine(SolutionPath, "Miscellaneous", "License.txt");

        private string Core20ReadmeFileName = "netcore20-agent-readme.md";
        private string ReadmeFilePath => Path.Combine(SolutionPath, "Miscellaneous", Core20ReadmeFileName);

        private string AgentApiPath => Path.Combine(AnyCpuBuildPath, "NewRelic.Api.Agent", _isCoreClr ? "netstandard2.0" : "net35", "NewRelic.Api.Agent.dll");

        private string _homeBuilderProjectPath => Path.Combine(SolutionPath, "NewRelic", "Installer", "New Relic Home Builder", "New Relic Home Builder.csproj");
        private string _coreProjectPath => Path.Combine(SolutionPath, "NewRelic", "Agent", "Core", "Core.csproj");

        private string AgentVersion => FileVersionInfo.GetVersionInfo(DestinationAgentFilePath).FileVersion;
        private string ProfilerDllPath
        {
            get
            {
                var profilerPath = Path.Combine(SolutionPath, "ProfilerBuildsForDevMachines", "Windows", Bitness, "NewRelic.Profiler.dll");
                return profilerPath;
            }
        }
        private string ProfilerSoPath
        {
            get
            {
                var folderPath = Path.Combine(SolutionPath, "ProfilerBuildsForDevMachines", "Linux", Bitness, "libNewRelicProfiler.so");
                var profilerSoPath = Path.Combine(folderPath, ProfilerSoFileName);

                return profilerSoPath;
            }
        }
        private String CoreBuildDirectoryPath { get { return Path.Combine(AnyCpuBuildPath, @"NewRelic.Agent.Core", _isCoreClr ? "netstandard2.0" : "net35"); } }
        private String NewRelicAgentExtensionsPath { get { return Path.Combine(CoreBuildDirectoryPath, "NewRelic.Agent.Extensions.dll"); } }
        private String KeyFilePath { get { return Path.Combine(SolutionPath, "NewRelicStrongNameKey.snk"); } }
        private String ExtensionsDirectoryPath { get { return Path.Combine(SolutionPath, "NewRelic", "Agent", "Extensions"); } }

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
            RepackAndCopyCoreAsembliesToDirectory(CoreBuildDirectoryPath, DestinationAgentFilePath, KeyFilePath);
            CopyToDirectory(NewRelicConfigPath, DestinationHomeDirectoryPath);
            CopyToDirectory(ExtensionsXsdPath, DestinationExtensionsDirectoryPath);
            CopyToDirectory(NewRelicAgentExtensionsPath, DestinationHomeDirectoryPath);
            CopyAgentExtensions();
            CopyOtherDependencies();

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

            if (!_isCoreClr)
            {
                return;
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
            if (_isCoreClr)
            {
                CopyToDirectory(LicenseFilePath, DestinationHomeDirectoryPath);
                CopyToDirectory(AgentApiPath, DestinationHomeDirectoryPath);
                CopyToDirectory(ReadmeFilePath, DestinationHomeDirectoryPath);
                File.Move(Path.Combine(DestinationHomeDirectoryPath, Core20ReadmeFileName), Path.Combine(DestinationHomeDirectoryPath, "README.md"));
                return;
            }
        }

        private static void ReCreateDirectoryWithEveryoneAccess(String directoryPath)
        {
            try { Directory.Delete(directoryPath, true); }
            catch (DirectoryNotFoundException) { }

            Thread.Sleep(TimeSpan.FromMilliseconds(1));

            var directoryInfo = Directory.CreateDirectory(directoryPath);
            var directorySecurity = directoryInfo.GetAccessControl();
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            directorySecurity.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directoryInfo.SetAccessControl(directorySecurity);
        }

        private void RepackAndCopyCoreAsembliesToDirectory(String sourceDirectoryPath, String destinationFilePath, String keyFilePath)
        {
            if (sourceDirectoryPath == null)
                throw new ArgumentNullException("sourceDirectoryPath");
            if (destinationFilePath == null)
                throw new ArgumentNullException("destinationFilePath");

            var assemblyPathsToRepack = new List<String> { Path.Combine(sourceDirectoryPath, @"NewRelic.Agent.Core.dll") };

            var coreAssemblies = Directory.GetFiles(sourceDirectoryPath)
                    .Where(filePath => filePath != null)
                    .Where(filePath => !filePath.EndsWith(@"NewRelic.Agent.Core.dll"))
                    .Where(filePath => !filePath.EndsWith(@"NewRelic.Agent.Extensions.dll"))
                    .Where(filePath => Path.GetExtension(filePath) == ".dll");

            assemblyPathsToRepack.AddRange(coreAssemblies);

            if (_isCoreClr)
            {
                var netstandardAssemblyPaths = GetNetstandardAssemblyPaths();
                assemblyPathsToRepack.AddRange(netstandardAssemblyPaths);

                if (Environment.GetEnvironmentVariable("NEWRELIC_INSTALL_PATH") != null)
                {
                    var homeDirectory = Directory.GetParent(destinationFilePath);
                    foreach (var dllPath in netstandardAssemblyPaths)
                    {
                        var destFileName = Path.Combine(homeDirectory.FullName, new FileInfo(dllPath).Name);
                        File.Copy(dllPath, destFileName);
                    }
                }
            }

            foreach (var assemblyPath in assemblyPathsToRepack)
            {
                Console.WriteLine($"[HomeBuilder]: attempting to repack assembly at: {assemblyPath}");
            }

            Console.WriteLine();

            var netStandardPath = Path.Combine(NuGetPackageDir, "NETStandard.Library", "2.0.0", "build", "netstandard2.0", "ref");
            var newtonsoftResolvePath = GetNugetPackageDllFolderPath(_coreProjectPath, "Newtonsoft.Json", VersionResolution.Latest, "lib", "netstandard1.3");

            Console.WriteLine($"[HomeBuilder]: Adding netstandard path for .NET Standard IL Repack resolution to: {netStandardPath}");
            Console.WriteLine($"[HomeBuilder]: Adding newtonsoft path for .NET Standard IL Repack resolution to: {newtonsoftResolvePath}");

            var ilRepackOptions = new RepackOptions(Enumerable.Empty<string>())
            {
                AllowDuplicateResources = false,
                AllowMultipleAssemblyLevelAttributes = false,
                AllowWildCards = false,
                AllowZeroPeKind = false,
                AttributeFile = null,
                CopyAttributes = false,
                DebugInfo = true,
                DelaySign = false,
                ExcludeFile = null,
                InputAssemblies = assemblyPathsToRepack.ToArray(),
                Internalize = true,
                KeepOtherVersionReferences = true,
                KeyFile = keyFilePath,
                LineIndexation = false,
                NoRepackRes = true,
                OutputFile = destinationFilePath,
                Parallel = true,
                PauseBeforeExit = false,
                SearchDirectories = new[] { sourceDirectoryPath, netStandardPath, newtonsoftResolvePath },
                TargetKind = ILRepack.Kind.SameAsPrimaryAssembly,
                UnionMerge = false,
                Version = null,
                XmlDocumentation = false,
            };

            var ilRepack = new ILRepack(ilRepackOptions);
            ilRepack.Repack();
        }

        private string GetNugetPackageDllPath(string csprojPath, string packageName, VersionResolution versionResolution, params string[] packageSubFolders)
        {
            var folderPath = GetNugetPackageDllFolderPath(csprojPath, packageName, versionResolution, packageSubFolders);
            var dllPath = Path.Combine(folderPath, $"{packageName}.dll") ?? String.Empty;

            return dllPath;
        }

        private string GetNugetPackageDllFolderPath(string csprojPath, string packageName, VersionResolution versionResolution, params string[] packageSubFolders)
        {
            var subFolderPath = Path.Combine(packageSubFolders);

            var regex = new Regex($@".*<PackageReference Include=""{packageName}"" Version=""(.*?)"" />");

            var versions = File.ReadAllLines(csprojPath)
                .Select(line => regex.Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups[1]);

            Console.WriteLine($"[HomeBuilder]: {packageName} Versions...");
            foreach (var v in versions)
            {
                Console.WriteLine($"[HomeBuilder]: {v}");
            }

            var version = GetVersion(versions, versionResolution);
            var packageFolder = $"{packageName}\\{version}";

            var folderPath = Path.Combine(NuGetPackageDir, packageFolder, subFolderPath) ?? String.Empty;

            return folderPath;
        }

        private string GetVersion(IEnumerable<Group> versions, VersionResolution versionResolution)
        {
            switch (versionResolution)
            {
                case VersionResolution.FirstFound:
                    return versions.First().Value;

                case VersionResolution.LastFound:
                    return versions.Last().Value;

                case VersionResolution.Earliest:
                    return versions.Min(v => new Version(v.Value)).ToString();

                case VersionResolution.Latest:
                    return versions.Max(v => new Version(v.Value)).ToString();

                default:
                    throw new ArgumentException($"Version resolution specified is not defined: {versionResolution}");
            }
        }

        private List<string> GetNetstandardAssemblyPaths()
        {
            var netstandardAssemblyPaths = new List<string>();

            var autofacDllPath = GetNugetPackageDllPath(_coreProjectPath, "Autofac", VersionResolution.FirstFound, "lib", "netstandard1.1");
            netstandardAssemblyPaths.Add(autofacDllPath);

            var log4netDllPath = GetNugetPackageDllPath(_coreProjectPath, "log4net", VersionResolution.Latest, "lib", "netstandard1.3");
            netstandardAssemblyPaths.Add(log4netDllPath);

            var moreLinqDllPath = GetNugetPackageDllPath(_coreProjectPath, "MoreLinq", VersionResolution.Latest, "lib", "netstandard1.0");
            netstandardAssemblyPaths.Add(moreLinqDllPath);

            var newtonSoftJsonDllPath = GetNugetPackageDllPath(_coreProjectPath, "Newtonsoft.Json", VersionResolution.Latest, "lib", "netstandard1.3");
            netstandardAssemblyPaths.Add(newtonSoftJsonDllPath);

            return netstandardAssemblyPaths;
        }

        private static void CopyToDirectory(String sourceFilePath, String destinationDirectoryPath)
        {
            if (sourceFilePath == null)
                throw new ArgumentNullException("sourceFilePath");
            if (destinationDirectoryPath == null)
                throw new ArgumentNullException("destinationDirectoryPath");

            CopyToDirectories(sourceFilePath, new[] { destinationDirectoryPath });
        }

        private static void CopyToDirectories(String sourceFilePath, IEnumerable<String> destinationDirectoryPaths)
        {
            if (sourceFilePath == null)
                throw new ArgumentNullException("sourceFilePath");
            if (destinationDirectoryPaths == null)
                throw new ArgumentNullException("destinationDirectoryPaths");

            var fileName = Path.GetFileName(sourceFilePath);
            destinationDirectoryPaths
                .Where(destinationDirectoryPath => destinationDirectoryPath != null)
                .Select(destinationDirectoryPath => Path.Combine(destinationDirectoryPath, fileName))
                .Where(destinationFilePath => destinationFilePath != null)
                .ToList()
                .ForEach(destinationFilePath => File.Copy(sourceFilePath, destinationFilePath, true));
        }

        private void CopyAgentExtensions()
        {
            var directoriesWithoutFramework = Directory.EnumerateDirectories(ExtensionsDirectoryPath, Configuration, SearchOption.AllDirectories);

            List<string> allDirectoriesForConfiguration = new List<String>(directoriesWithoutFramework);

            foreach (var directory in directoriesWithoutFramework)
            {
                var frameworkSubDirectories = Directory.EnumerateDirectories(directory, "*net*");
                allDirectoriesForConfiguration.AddRange(frameworkSubDirectories);
            }

            var directories = allDirectoriesForConfiguration.ToList()
                .Where(directoryPath => directoryPath != null)
                .Where(directoryPath => directoryPath.Contains("netstandard") == _isCoreClr)
                .Select(directoryPath => new DirectoryInfo(directoryPath))
                .Where(directoryInfo => directoryInfo.Parent != null)
                .Where(directoryInfo => directoryInfo.Parent.Name == "bin" || directoryInfo.Parent.Name == Configuration);

            var dlls = directories
                .SelectMany(directoryInfo => directoryInfo.EnumerateFiles("*.dll"))
                .Where(fileInfo => fileInfo != null)
                .DistinctBy(fileInfo => fileInfo.Name)
                .Where(fileInfo => fileInfo != null)
                .Select(fileInfo => fileInfo.FullName)
                .Where(filePath => filePath != null)
                .Where(filePath => FileVersionInfo.GetVersionInfo(filePath).FileVersion == AgentVersion);

            dlls.ForEach(filePath =>
            {
                CopyNewRelicAssemblies(filePath, DestinationExtensionsDirectoryPath);
                TryCopyExtensionInstrumentationFile(filePath, DestinationExtensionsDirectoryPath);
            });
        }

        private static void CopyNewRelicAssemblies(String assemblyFilePath, String destinationExtensionsDirectoryPath)
        {
            var directoryPath = Path.GetDirectoryName(assemblyFilePath);
            if (directoryPath == null)
                return;

            var directoryInfo = new DirectoryInfo(directoryPath);

            directoryInfo
                .EnumerateFiles("NewRelic.*.dll")
                .Where(fileInfo => fileInfo != null)
                .Where(fileInfo => fileInfo.Name != "NewRelic.Agent.Extensions.dll")
                .Where(fileInfo => !fileInfo.Name.EndsWith("Tests.dll"))
                .Select(fileInfo => fileInfo.FullName)
                .Where(filePath => filePath != null)
                .ForEach(filePath => CopyToDirectory(filePath, destinationExtensionsDirectoryPath));
        }

        private static void TryCopyExtensionInstrumentationFile(String assemblyFilePath, String destinationExtensionsDirectoryPath)
        {
            var directory = Path.GetDirectoryName(assemblyFilePath);

            if (directory == null)
                return;

            var instrumentationFilePath = Path.Combine(directory, "Instrumentation.xml");
            if (!File.Exists(instrumentationFilePath))
                return;

            var assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);
            if (assemblyName == null || !assemblyName.StartsWith("NewRelic"))
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
                String.Format(@"COR_PROFILER_PATH={0}", DestinationProfilerDllPath),
                String.Format(@"NEWRELIC_HOME={0}\", DestinationHomeDirectoryPath)
            };

            var bytes = new List<Byte>();
            foreach (var @string in strings)
            {
                if (@string == null)
                    continue;
                bytes.AddRange(Encoding.Unicode.GetBytes(@string));
                bytes.AddRange(new Byte[] { 0, 0 });
            }
            bytes.AddRange(new Byte[] { 0, 0 });

            var hexString = BitConverter.ToString(bytes.ToArray()).Replace('-', ',');
            const string fileContentsFormatter =
@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\W3SVC]
""Environment""=hex(7):{0}

[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WAS]
""Environment""=hex(7):{0}
";
            var fileContents = String.Format(fileContentsFormatter, hexString);
            File.WriteAllText(DestinationRegistryFilePath, fileContents);
        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.ParseCommandLineArguments(args);

            var op = args[0];
            switch (op)
            {
                case "buildCoreArtifactsForS3Deploy":
                    program.BuildCoreArtifactsForS3Deploy();
                    break;
                default:
                    program.RealMain();
                    break;
            }
        }

        private void ParseCommandLineArguments(String[] commandLineArguments)
        {
            var defaultParser = CommandLine.Parser.Default;
            if (defaultParser == null)
                throw new NullReferenceException("defaultParser");

            defaultParser.ParseArgumentsStrict(commandLineArguments, this);
        }

        private string CoreArtifactRootDirectory => Path.Combine(BuildOutputPath, "CoreArtifacts");

        private void BuildCoreArtifactsForS3Deploy()
        {
            // _isCoreClr needs to be set accordingly in order to utilizize existing code paths as part of the
            // creation of the core installer.
            _isCoreClr = true;

            PrepareWorkspace();
            CreateCoreClrArchives();
            BuildCoreInstaller();
            CopyCoreReadme();
        }

        private void PrepareWorkspace()
        {
            if (Directory.Exists(CoreArtifactRootDirectory))
            {
                Directory.Delete(CoreArtifactRootDirectory, true);
            }

            if (Directory.Exists(CoreInstallerOutputPath))
            {
                Directory.Delete(CoreInstallerOutputPath, true);
            }

            Directory.CreateDirectory(CoreArtifactRootDirectory);
            Directory.CreateDirectory(CoreInstallerOutputPath);
        }

        private void CopyCoreReadme()
        {
            var dstReadme = Path.Combine(CoreArtifactRootDirectory, Core20ReadmeFileName);
            File.Copy(ReadmeFilePath, dstReadme, true);
            File.Move(dstReadme, Path.Combine(CoreArtifactRootDirectory, "README.md"));
        }

        private void CreateCoreClrArchives()
        {
            CreateCoreClrArchive("x86");
            CreateCoreClrArchive("x64");
        }

        private void CreateCoreClrArchive(string bitness)
        {
            Bitness = bitness;

            if (!Directory.Exists(DestinationHomeDirectoryPath))
            {
                throw new Exception($"Home directory does not exist [{DestinationHomeDirectoryPath}]. Build it first.");
            }

            var zipFileName = $"newrelic-netcore20-agent-win_{AgentVersion}_{Bitness}.zip";
            var zipFilePath = Path.Combine(CoreArtifactRootDirectory, zipFileName);

            Console.WriteLine($"[HomeBuilder]: Zipping contents of {DestinationHomeDirectoryPath}");
            Console.WriteLine($"[HomeBuilder]: Saving zipped contents to {zipFilePath}");

            System.IO.Compression.ZipFile.CreateFromDirectory(DestinationHomeDirectoryPath, zipFilePath);
        }

        public void BuildCoreInstaller()
        {
            var bitnesses = new List<string>() { "x64", "x86" };

            var scriptPath = Path.Combine(CoreInstallerSourcePath, "installAgent.ps1");
            var usageTextPath = Path.Combine(CoreInstallerSourcePath, "installAgentUsage.txt");

            var installerZipFile = $"newrelic-netcore20-agent-win-installer_{AgentVersion}.zip";
            var installerZipPath = Path.Combine(CoreArtifactRootDirectory, installerZipFile);

            Console.WriteLine($"[HomeBuilder]: Copying core installer files to  {CoreInstallerOutputPath}");
            CopyToDirectory(scriptPath, CoreInstallerOutputPath);
            CopyToDirectory(usageTextPath, CoreInstallerOutputPath);

            foreach (var bitness in bitnesses)
            {
                var zipFileName = $"newrelic-netcore20-agent-win_{AgentVersion}_{bitness}.zip";
                var zipFilePath = Path.Combine(CoreArtifactRootDirectory, zipFileName);
                var unzipPath = Path.Combine(CoreInstallerOutputPath, bitness);
                Console.WriteLine($"[HomeBuilder]: Copying {bitness} core zip files to  {CoreInstallerOutputPath}");

                System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, unzipPath);

                var linuxProfilerPath = Path.Combine(unzipPath, ProfilerSoFileName);
                if (File.Exists(linuxProfilerPath))
                {
                    File.Delete(linuxProfilerPath);
                }
            }

            Console.WriteLine($"[HomeBuilder]: Compressing core installer files to  {installerZipPath}");

            System.IO.Compression.ZipFile.CreateFromDirectory(CoreInstallerOutputPath, installerZipPath);

            Console.WriteLine($"[HomeBuilder]: Removing core installer files from working directory  {CoreInstallerOutputPath}");

            Directory.Delete(CoreInstallerOutputPath, true);
        }
    }
}
