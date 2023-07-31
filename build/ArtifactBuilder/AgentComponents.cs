using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArtifactBuilder.Artifacts;

namespace ArtifactBuilder
{
    public abstract class AgentComponents
    {
        public AgentComponents(string configuration, string platform, string repoRootDirectory, string homeRootPath)
        {
            Configuration = configuration;
            Platform = platform;
            SourcePath = $@"{repoRootDirectory}\src\Agent";
            HomeRootPath = homeRootPath;
        }

        public string Configuration { get; }

        public static AgentComponents GetAgentComponents(AgentType agentType, string configuration, string platform, string repoRootDirectory, string homeRootPath)
        {
            AgentComponents agentComponents;
            switch (agentType)
            {
                case AgentType.Framework:
                    agentComponents = new FrameworkAgentComponents(configuration, platform, repoRootDirectory, homeRootPath);
                    break;
                case AgentType.Core:
                    agentComponents = new CoreAgentComponents(configuration, platform, repoRootDirectory, homeRootPath);
                    break;
                default:
                    throw new Exception("Invalid AgentType");
            }
            agentComponents.CreateAgentComponents();
            return agentComponents;
        }

        public string Platform { get; }
        public string SourcePath { get; }
        public string HomeRootPath { get; }
        public IReadOnlyCollection<string> ExtensionDirectoryComponents { get; private set; }
        public IReadOnlyCollection<string> WrapperXmlFiles { get; private set; }
        public IReadOnlyCollection<string> RootInstallDirectoryComponents { get; private set; }
        public IReadOnlyCollection<string> AgentHomeDirComponents { get; private set; }
        public IReadOnlyCollection<string> ConfigurationComponents { get; private set; }
        public string AgentApiDll;
        public string WindowsProfiler;
        public string LinuxProfiler;
        public string ExtensionXsd;
        public string NewRelicXsd;
        public string NewRelicConfig;
        public string NewRelicLicenseFile;
        public string NewRelicThirdPartyNoticesFile;
        public string AgentInfoJson;
        public string[] GRPCExtensionsLibWindows;

        public List<string> AllComponents
        {
            get
            {
                var list = RootInstallDirectoryComponents
                    .Concat(AgentHomeDirComponents)
                    .Concat(ExtensionDirectoryComponents)
                    .Concat(WrapperXmlFiles)
                    .Append(ExtensionXsd)
                    .Append(AgentApiDll)
                    .ToList();

                if (!string.IsNullOrEmpty(LinuxProfiler))
                {
                    list.Add(LinuxProfiler);
                }

                return list;
            }
        }

        public string Version
        {
            get
            {
                try
                {
                    return System.Diagnostics.FileVersionInfo.GetVersionInfo(AgentApiDll).FileVersion;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string SemanticVersion
        {
            get
            {
                return new Version(Version).ToString(3);
            }
        }

        // This will return the full four-component version string, unless the fourth component is zero, in which case it returns three components
        public string MaybeSemanticVersion
        {
            get
            {
                var version = new Version(Version);
                if (version.Revision == 0)
                {
                    return version.ToString(3);
                }
                else
                {
                    return version.ToString(4);
                }
            }
        }

        protected abstract string SourceHomeBuilderPath { get; }
        protected abstract List<string> IgnoredHomeBuilderFiles { get; }
        protected abstract void CreateAgentComponents();

        protected void SetConfigurationComponents(params string[] items)
        {
            ConfigurationComponents = new HashSet<string>(items);
        }

        protected void SetRootInstallDirectoryComponents(params string[] items)
        {
            RootInstallDirectoryComponents = new HashSet<string>(items);
        }
        protected void SetAgentHomeDirComponents(params string[] items)
        {
            AgentHomeDirComponents = new HashSet<string>(items);
        }

        protected void SetWrapperXmlFiles(params string[] items)
        {
            WrapperXmlFiles = new HashSet<string>(items);
        }

        protected void SetExtensionDirectoryComponents(params string[] items)
        {
            ExtensionDirectoryComponents = new HashSet<string>(items);
        }

        public void CopyComponents(string destinationDirectory, string agentTypeSubDirectory = null)
        {
            FileHelpers.CopyFile(RootInstallDirectoryComponents, destinationDirectory);
            var agentTypeInstallDirectory = destinationDirectory;
            if (agentTypeSubDirectory != null)
            {
                agentTypeInstallDirectory = Path.Combine(destinationDirectory, agentTypeSubDirectory);
            }
            FileHelpers.CopyFile(AgentHomeDirComponents, agentTypeInstallDirectory);
            FileHelpers.CopyFile(ExtensionDirectoryComponents, $@"{agentTypeInstallDirectory}\extensions");
            FileHelpers.CopyFile(WrapperXmlFiles, $@"{agentTypeInstallDirectory}\extensions");
        }

        public void ValidateComponents()
        {
            CheckForMissingComponents();
            CheckForXmlFilesThatAreNotUtf8();
            CheckForMissingFilesInHomeBuilderDirectory();
            CheckForRequiredProperties();
        }

        private void LogAndThrow(string msg, IList<string> files)
        {
            if (files.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(msg);
                sb.AppendLine();
                foreach (var item in files)
                {
                    sb.AppendLine(item);
                }
                throw new PackagingException(sb.ToString());
            }
        }

        private void CheckForMissingComponents()
        {
            var missingComponents = new List<string>();

            foreach (var item in AllComponents)
            {
                if (!File.Exists(item)) missingComponents.Add(item);
            }

            LogAndThrow($@"Missing components - make sure you have built the agent for {Platform}-{Configuration}", missingComponents);
        }

        private void CheckForXmlFilesThatAreNotUtf8()
        {
            var utf8NoBom = new UTF8Encoding(false);

            var nonUtf8Files = new List<string>();

            var xmlFiles = new List<string>(WrapperXmlFiles);
            xmlFiles.Add(ExtensionXsd);
            xmlFiles.Add(NewRelicXsd);
            xmlFiles.Add(NewRelicConfig);

            foreach (var xmlFile in xmlFiles)
            {
                using (var sr = new StreamReader(xmlFile, utf8NoBom))
                {
                    sr.Read();
                    if (!Equals(sr.CurrentEncoding, utf8NoBom))
                    {
                        nonUtf8Files.Add(xmlFile);
                    }
                }
            }

            LogAndThrow("xml/xsd files that ship with the agent must be UTF-8 (no BOM) - the following files are either not UTF-8 or contain a BOM", nonUtf8Files);
        }

        private void CheckForMissingFilesInHomeBuilderDirectory()
        {
            var missingComponents = new List<string>();
            var homeBuilderFiles = Directory.EnumerateFiles(SourceHomeBuilderPath, "*.*", SearchOption.AllDirectories);
            homeBuilderFiles = homeBuilderFiles.Where(x => !x.Contains(@"\Logs\"));

            foreach (var file in homeBuilderFiles)
            {
                if (!AllComponents.Contains(file) && !IgnoredHomeBuilderFiles.Contains(file))
                {
                    missingComponents.Add(file);
                }
            }

            LogAndThrow("There are additional files in the Home Builder directory that are not specified in this artifact's component set:", missingComponents);
        }

        private void CheckForRequiredProperties()
        {
            var requiredProperties = new Dictionary<string, string>();
            if (Platform != "arm64") 
            { 
                requiredProperties.Add(nameof(WindowsProfiler), WindowsProfiler);
            }
            var missingPropertyNames = new List<string>();

            foreach (var requiredProperty in requiredProperties)
            {
                if (string.IsNullOrEmpty(requiredProperty.Value))
                {
                    missingPropertyNames.Add(requiredProperty.Key);
                }
            }

            LogAndThrow($"The following properties were not specified for {Platform}-{Configuration}:", missingPropertyNames);
        }
    }
}
