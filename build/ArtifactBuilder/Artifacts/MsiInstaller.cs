using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace ArtifactBuilder.Artifacts
{
    class MsiInstaller : Artifact
    {
        private string[] _frameworkIISRegistryValues = new string[] {
                    "COR_ENABLE_PROFILING=1",
                    "COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}" };

        private string[] _coreIISRegistryValues = new string[] {
                    "CORECLR_ENABLE_PROFILING=1",
                    "CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}",
                    "CORECLR_NEWRELIC_HOME=[NETAGENTCOMMONFOLDER]" };

        public string Configuration { get; }
        public string Platform { get; }
        public string MsiDirectory { get; }

        public MsiInstaller(string platform, string configuration) : base("MsiInstaller")
        {
            Platform = platform;
            Configuration = configuration;
            MsiDirectory = $@"{SourceDirectory}\src\_build\{Platform}-{Configuration}\Installer";
            OutputDirectory = $@"{SourceDirectory}\build\BuildArtifacts\{Name}-{Platform}";
        }

        protected override void InternalBuild()
        {
            if (!Directory.Exists(MsiDirectory))
            {
                Console.WriteLine("Warning: The {0} directory does not exist.", MsiDirectory);
                return;
            }

            //ValidateWxsDefinitionFileForInstaller();

            var fileSearchPattern = $@"NewRelicAgent_{Platform}_*.msi";
            var msiPath = Directory.GetFiles(MsiDirectory, fileSearchPattern).FirstOrDefault();

            if (string.IsNullOrEmpty(msiPath))
            {
                Console.WriteLine("Warning: The {0} installer could not be found.", fileSearchPattern);
                return;
            }

            FileHelpers.CopyFile(msiPath, OutputDirectory);
            File.WriteAllText($@"{OutputDirectory}\checksum.sha256", FileHelpers.GetSha256Checksum(msiPath));
        }

        private void ValidateWxsDefinitionFileForInstaller()
        {
            //Verify that the expected agent components are in the homebuilder for the installer
            var frameworkAgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, SourceDirectory);
            frameworkAgentComponents.ValidateComponents();

            var coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, Platform, SourceDirectory);
            coreAgentComponents.ValidateComponents();

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

            ValidateAgentComponentsAndWixReferenceTheSameFiles(frameworkAgentComponents.ExtensionDirectoryComponents, extensionGroup, frameworkExtensionsComponentsGroup);
            ValidateAgentComponentsAndWixReferenceTheSameFiles(frameworkAgentComponents.WrapperXmlFiles, instrumentationGroup);




            ValidateAgentComponentsAndWixReferenceTheSameFiles(frameworkAgentComponents.ConfigurationComponents, frameworkConfigurationComponentsGroup);

            ValidateAgentComponentsAndWixReferenceTheSameFiles(CreateReadOnlyCollection(coreAgentComponents.AgentApiDll), globalApiComponentGroup);
            ValidateAgentComponentsAndWixReferenceTheSameFiles(CreateReadOnlyCollection(frameworkAgentComponents.AgentApiDll), globalApiComponentGroup);
        }

        private Wix GetParsedProductWxsData()
        {
            using (var xmlReader = XmlReader.Create($@"{SourceDirectory}\src\Agent\MsiInstaller\Installer\Product.wxs"))
            {
                var serializer = new XmlSerializer(typeof(Wix));
                return (Wix)serializer.Deserialize(xmlReader);
            }
        }

        private void ValidateWixFileExtensionDefinitions(WixFragmentComponentGroup group, bool isCore = false)
        {
            foreach (var component in group.Component)
            {
                var file = component.File;
                if (file.KeyPath != "yes")
                {
                    throw new PackagingException($"Product.wxs file {file.Id} did not have KeyPath set to yes, but was {file.KeyPath}.");
                }

                var expectedSourcePath = isCore ? $@"$(var.SolutionDir)New Relic Home $(var.Platform) CoreClr\Extensions\{file.Name}" : $@"$(var.SolutionDir)New Relic Home $(var.Platform)\Extensions\{file.Name}";
                if (file.Source != expectedSourcePath)
                {
                    throw new PackagingException($"Product.wxs file {file.Id} did not have the expected source path of {expectedSourcePath}, but was {file.Source}");
                }
            }
        }

        private void ValidateWixRegistryDefinitions(WixFragmentComponentGroup group)
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

        private void ValidateWixUninstallComponents(WixFragmentComponentGroupComponent uninstallComponent)
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

                if (component.RegistryValue.MultiStringValue.Count != 3)
                {
                    throw new PackagingException($@"Product.wxs Framework registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have correct number of string values: expected 3 was {component.RegistryValue.MultiStringValue.Count}.");
                }

                foreach (var value in _frameworkIISRegistryValues)
                {
                    if (!component.RegistryValue.MultiStringValue.Contains(value))
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

                if (component.RegistryValue.MultiStringValue.Count != 4)
                {
                    throw new PackagingException($@"Product.wxs Core registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} did not have correct number of string values: expected 4 was {component.RegistryValue.MultiStringValue.Count}.");
                }

                foreach (var value in _coreIISRegistryValues)
                {
                    if (!component.RegistryValue.MultiStringValue.Contains(value))
                    {
                        throw new PackagingException($@"Product.wxs Core registryvalue {component.RegistryValue.Name}\{component.RegistryValue.Name} missing {value}");
                    }
                }
            }
        }
        private void ValidateAgentComponentsAndWixReferenceTheSameFiles(IReadOnlyCollection<string> agentComponent, params WixFragmentComponentGroup[] wixGroups)
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
    }
}
