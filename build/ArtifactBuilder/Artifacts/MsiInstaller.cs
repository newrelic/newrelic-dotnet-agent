using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace ArtifactBuilder.Artifacts
{
    class MsiInstaller : Artifact
    {
        private readonly string[] _frameworkIISRegistryValues = new string[] {
                    "COR_ENABLE_PROFILING=1",
                    "COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}" };

        private readonly string[] _coreIISRegistryValues = new string[] {
                    "CORECLR_ENABLE_PROFILING=1",
                    "CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}",
                    "CORECLR_NEWRELIC_HOME=[NETAGENTCOMMONFOLDER]" };

        private readonly AgentComponents _frameworkAgentComponents;
        private readonly AgentComponents _coreAgentComponents;

        public string Configuration { get; }
        public string Platform { get; }
        public string MsiDirectory { get; }

        public MsiInstaller(string platform, string configuration) : base("MsiInstaller")
        {
            Platform = platform;
            Configuration = configuration;
            MsiDirectory = $@"{RepoRootDirectory}\src\_build\{Platform}-{Configuration}\Installer";
            OutputDirectory = $@"{RepoRootDirectory}\build\BuildArtifacts\{Name}-{Platform}";
            ValidateContentAction = ValidateContent;

            _frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, Platform, RepoRootDirectory, HomeRootDirectory);
        }

        protected override void InternalBuild()
        {
            if (!Directory.Exists(MsiDirectory))
            {
                Console.WriteLine("Warning: The {0} directory does not exist.", MsiDirectory);
                return;
            }

            ValidateWxsDefinitionFileForInstaller();

            if (TryGetMsiPath(out var msiPath))
            {
                ValidateCodeSigningCertificate(msiPath);

                FileHelpers.CopyFile(msiPath, OutputDirectory);
                File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(msiPath));
            }
        }

        private void ValidateWxsDefinitionFileForInstaller()
        {
            //Verify that the expected agent components are in the homebuilder for the installer
            _frameworkAgentComponents.ValidateComponents();
            _coreAgentComponents.ValidateComponents();

            var productWxs = GetParsedProductWxsData();

            // Framework
            var extensionGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "NewRelic.Agent.Extensions");
            var instrumentationGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "NewRelic.Agent.Extensions.Instrumentation");
            var frameworkExtensionsComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "ExtensionsComponents");
            var frameworkConfigurationComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "ConfigurationComponents");
            var frameworkRegistryComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "RegistryComponents");
            var frameworkProgramsComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "ProgramsComponents");
            var frameworkIISRegistryComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "IISRegistryComponents");
            var frameworkProductComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "ProductComponents");

            // Core
            var coreExtensionGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "CoreNewRelic.Agent.Extensions");
            var coreInstrumentationGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "CoreNewRelic.Agent.Extensions.Instrumentation");
            var coreIISRegistryComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "CoreIISRegistryComponents");
            var coreProductComponentsGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "CoreProductComponents");

            // All installs
            var uninstallComponent = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "UninstallComponents")
                .Component.First();
            var globalApiComponentGroup = productWxs.Fragment.Items
                .OfType<WixFragmentComponentGroup>()
                .First(cg => cg.Id == "ApiComponents");


            ValidateWixFileExtensionDefinitions(extensionGroup);
            ValidateWixFileExtensionDefinitions(coreExtensionGroup, isCore: true);
            ValidateWixFileExtensionDefinitions(instrumentationGroup);
            ValidateWixFileExtensionDefinitions(coreInstrumentationGroup, isCore: true);

            ValidateWixFileExtensionDefinitions(frameworkExtensionsComponentsGroup);

            ValidateWixRegistryDefinitions(frameworkRegistryComponentsGroup);

            ValidateWixRegistryDefinitions(frameworkProgramsComponentsGroup);

            ValidateWixRegistryDefinitions(frameworkProductComponentsGroup);
            ValidateWixRegistryDefinitions(coreProductComponentsGroup);

            ValidateWixUninstallComponents(uninstallComponent);
            ValidateWixIisRegistryDefinitions(frameworkIISRegistryComponentsGroup, coreIISRegistryComponentsGroup);

            ValidateAgentComponentsAndWixReferenceTheSameFiles(_frameworkAgentComponents.ExtensionDirectoryComponents, extensionGroup, frameworkExtensionsComponentsGroup);
            ValidateAgentComponentsAndWixReferenceTheSameFiles(_frameworkAgentComponents.WrapperXmlFiles, instrumentationGroup);




            ValidateAgentComponentsAndWixReferenceTheSameFiles(_frameworkAgentComponents.ConfigurationComponents, frameworkConfigurationComponentsGroup);

            ValidateAgentComponentsAndWixReferenceTheSameFiles(CreateReadOnlyCollection(_coreAgentComponents.AgentApiDll), globalApiComponentGroup);
            ValidateAgentComponentsAndWixReferenceTheSameFiles(CreateReadOnlyCollection(_frameworkAgentComponents.AgentApiDll), globalApiComponentGroup);
        }

        private Wix GetParsedProductWxsData()
        {
            using (var xmlReader = XmlReader.Create($@"{RepoRootDirectory}\src\Agent\MsiInstaller\Installer\Product.wxs"))
            {
                var serializer = new XmlSerializer(typeof(Wix));
                return (Wix)serializer.Deserialize(xmlReader);
            }
        }

        private static void ValidateWixFileExtensionDefinitions(WixFragmentComponentGroup group, bool isCore = false)
        {
            foreach (var component in group.Component)
            {
                var file = component.File;
                if (file.KeyPath != "yes")
                {
                    throw new PackagingException($"Product.wxs file {file.Id} did not have KeyPath set to yes, but was {file.KeyPath}.");
                }

                var expectedSourcePath = isCore ? $@"$(var.HomeFolderPath)_coreclr\extensions\{file.Name}" : $@"$(var.HomeFolderPath)\extensions\{file.Name}";
                if (file.Source != expectedSourcePath)
                {
                    throw new PackagingException($"Product.wxs file {file.Id} did not have the expected source path of {expectedSourcePath}, but was {file.Source}");
                }
            }
        }

        private static void ValidateWixRegistryDefinitions(WixFragmentComponentGroup group)
        {
            foreach (var component in group.Component)
            {
                var registryValue = component.RegistryValue;
                if (registryValue == null)
                {
                    continue;
                }

                if (registryValue.KeyPath != "yes")
                {
                    throw new PackagingException($"Product.wxs registryvalue {registryValue.Name} did not have KeyPath set to yes, but was {registryValue.KeyPath}.");
                }

                var expectedKey = $@"Software\New Relic\.NET Agent";
                if (registryValue.Key != expectedKey)
                {
                    throw new PackagingException($"Product.wxs registryvalue {registryValue.Name} did not have the expected source path of {expectedKey}, but was {registryValue.Key}");
                }
            }
        }

        private static void ValidateWixUninstallComponents(WixFragmentComponentGroupComponent uninstallComponent)
        {
            if (uninstallComponent.RegistryValue.KeyPath != "yes")
            {
                throw new PackagingException($"Product.wxs UninstallComponents registryvalue {uninstallComponent.RegistryValue.Name} did not have KeyPath set to yes, but was {uninstallComponent.RegistryValue.KeyPath}.");
            }

            if (uninstallComponent.RegistryValue.Name != "NetAgentUninstallShortcutInstalled")
            {
                throw new PackagingException($@"Product.wxs UninstallComponents did not have a valid RegistryValue named 'NetAgentUninstallShortcutInstalled'.");
            }

            if (uninstallComponent.RegistryValue.Key != @"Software\New Relic")
            {
                throw new PackagingException($@"Product.wxs UninstallComponents did not have a valid RegistryValue with a Key of 'Software\New Relic'.");
            }
        }

        private void ValidateWixIisRegistryDefinitions(WixFragmentComponentGroup frameworkGroup, WixFragmentComponentGroup coreGroup)
        {
            // Framework
            foreach (var component in frameworkGroup.Component)
            {
                if (component.RegistryValue.KeyPath != "yes")
                {
                    throw new PackagingException($@"Product.wxs Framework registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have KeyPath set to yes, but was {component.RegistryValue.KeyPath}.");
                }

                if (component.RegistryValue.MultiStringValue.Count != 4)
                {
                    throw new PackagingException($@"Product.wxs Framework registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have correct number of string values: expected 4 was {component.RegistryValue.MultiStringValue.Count}.");
                }

                foreach (var value in _frameworkIISRegistryValues)
                {
                    if (!component.RegistryValue.MultiStringValue.Any(msv => msv.Value == value))
                    {
                        throw new PackagingException($@"Product.wxs Framework registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} missing {value}");
                    }
                }
            }

            // Core
            foreach (var component in coreGroup.Component)
            {
                if (component.RegistryValue.KeyPath != "yes")
                {
                    throw new PackagingException($@"Product.wxs Core registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have KeyPath set to yes, but was {component.RegistryValue.KeyPath}.");
                }

                if (component.RegistryValue.MultiStringValue.Count != 5)
                {
                    throw new PackagingException($@"Product.wxs Core registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have correct number of string values: expected 5 was {component.RegistryValue.MultiStringValue.Count}.");
                }

                foreach (var value in _coreIISRegistryValues)
                {
                    if (!component.RegistryValue.MultiStringValue.Any(msv => msv.Value == value))
                    {
                        throw new PackagingException($@"Product.wxs Core registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} missing {value}");
                    }
                }
            }
        }
        private static void ValidateAgentComponentsAndWixReferenceTheSameFiles(IReadOnlyCollection<string> agentComponent, params WixFragmentComponentGroup[] wixGroups)
        {
            var wixFiles = new List<string>();
            foreach (var wixGroup in wixGroups)
            {
                wixFiles.AddRange(wixGroup.Component.Where(c => c.File != null).Select(c => c.File.Name).ToList());
            }

            var agentComponentFiles = agentComponent.Select(Path.GetFileName);

            foreach (var expectedFile in agentComponentFiles)
            {
                if (!wixFiles.Contains(expectedFile, StringComparer.InvariantCultureIgnoreCase))
                {
                    throw new PackagingException($"Product.wxs is missing file {expectedFile}.");
                }
            }

            foreach (var expectedFile in wixFiles)
            {
                if (!agentComponentFiles.Contains(expectedFile, StringComparer.InvariantCultureIgnoreCase))
                {
                    throw new PackagingException($"Product.wxs contains file {expectedFile} not found in the agent components.");
                }
            }
        }

        private static IReadOnlyCollection<string> CreateReadOnlyCollection(params string[] items)
        {
            return new HashSet<string>(items);
        }

        private bool TryGetMsiPath(out string msiPath)
        {
            var fileSearchPattern = $@"NewRelicAgent_{Platform}_{_frameworkAgentComponents.Version}.msi";
            msiPath = Directory.GetFiles(MsiDirectory, fileSearchPattern).FirstOrDefault();

            if (string.IsNullOrEmpty(msiPath))
            {
                Console.WriteLine("Warning: The {0} installer could not be found.", fileSearchPattern);
                return false;
            }

            return true;
        }

        private void ValidateContent()
        {
            var unpackedLocation = Unpack();

            if (string.IsNullOrEmpty(unpackedLocation))
            {
                return;
            }

            // Wix v4+ admin install places files into proper subdirectories instead of a single directory
            // Combining the new directories results in the same structure from Wix v3 for less changes
            var commAppRoot = Path.Join(unpackedLocation, "CommApp", "New Relic", ".NET Agent");
            var pFilesRootx86 = Path.Join(unpackedLocation, "PFiles", "New Relic", ".NET Agent");
            var pFilesRootx64 = Path.Join(unpackedLocation, "PFiles64", "New Relic", ".NET Agent");
            var installedFilesRoot = Path.Join(unpackedLocation, "New Relic", ".NET Agent");

            FileHelpers.CopyAll(commAppRoot, installedFilesRoot);
            FileHelpers.CopyAll(pFilesRootx86, installedFilesRoot); // needed for both x86 and x64
            if (Platform == "x64")
            {
                FileHelpers.CopyAll(pFilesRootx64, installedFilesRoot);
            }

            var expectedComponents = GetExpectedComponents(installedFilesRoot);

            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(installedFilesRoot);

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, Platform + " msi");

            FileHelpers.DeleteDirectories(unpackedLocation);
        }

        private string Unpack()
        {
            if (!TryGetMsiPath(out var msiPath))
            {
                return string.Empty;
            }

            var unpackedDirectory = Path.Join(OutputDirectory, "unpacked");

            if (Directory.Exists(unpackedDirectory))
            {
                FileHelpers.DeleteDirectories(unpackedDirectory);
            }

            var parameters = $@"/a ""{msiPath}"" /qn TARGETDIR=""{unpackedDirectory}""";
            var process = Process.Start("msiexec.exe", parameters);
            process.WaitForExit(30000);
            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception($"msiexec failed to complete in timely fashion.");
            }
            if (process.ExitCode != 0)
            {
                throw new Exception($"msiexec failed with exit code {process.ExitCode}.");
            }

            return unpackedDirectory;
        }

        private SortedSet<string> GetExpectedComponents(string installedFilesRoot)
        {
            var expectedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // The msi contains a different config file name than is used by the agent in the root directory
            // During a real installation the default config should be renamed.
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "default_newrelic.config");
            // The msi contains the agent api dll in the root probably for backwards compatibility
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, _frameworkAgentComponents.AgentApiDll);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _frameworkAgentComponents.RootInstallDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _frameworkAgentComponents.ConfigurationComponents);

            if (Platform == "x64")
            {
                // Only the x64 msi contains the profiler dll in the root probably for backwards compatibility
                ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, _frameworkAgentComponents.WindowsProfiler);
            }

            var installedExtensionsRoot = Path.Join(installedFilesRoot, "Extensions");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedExtensionsRoot, _frameworkAgentComponents.ExtensionXsd);

            var installedCoreExtensionXmlFolder = Path.Join(installedExtensionsRoot, "netcore");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedCoreExtensionXmlFolder, _coreAgentComponents.WrapperXmlFiles);

            var installedFrameworkExtensionsXmlFolder = Path.Join(installedExtensionsRoot, "netframework");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFrameworkExtensionsXmlFolder, _frameworkAgentComponents.WrapperXmlFiles);

            var netcoreFolder = Path.Join(installedFilesRoot, "netcore");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreFolder, _coreAgentComponents.AgentHomeDirComponents.Where(f => !f.EndsWith(".config") && !f.EndsWith(".xsd")));

            var netframeworkFolder = Path.Join(installedFilesRoot, "netframework");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netframeworkFolder, _frameworkAgentComponents.AgentHomeDirComponents.Where(f => !f.EndsWith(".config") && !f.EndsWith(".xsd")));

            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, netframeworkFolder, _frameworkAgentComponents.AgentApiDll);

            var netcoreExtensionsFolder = Path.Join(netcoreFolder, "Extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.ExtensionDirectoryComponents.Where(f => !f.EndsWith(".xsd")));

            var netframeworkExtensionsFolder = Path.Join(netframeworkFolder, "Extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netframeworkExtensionsFolder, _frameworkAgentComponents.ExtensionDirectoryComponents.Where(f => !f.EndsWith(".xsd")));

            // This script is only included with the msi installer
            expectedComponents.Add(Path.Combine(netframeworkFolder, "Tools", "flush_dotnet_temp.cmd"));

            return expectedComponents;
        }

        private void ValidateCodeSigningCertificate(string msiPath)
        {
            if (!SecurityHelpers.VerifyEmbeddedSignature(msiPath, out var errorMessage))
                throw new PackagingException($"Code signing certificate is not valid. {errorMessage}");
        }
    }
}
