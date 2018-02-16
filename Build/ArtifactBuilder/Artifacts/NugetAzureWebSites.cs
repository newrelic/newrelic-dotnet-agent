using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
    public class NugetAzureWebSites
    {
        public NugetAzureWebSites(string platform, string configuration, string sourceDirectory, NugetPushInfo nugetPushInfo)
        {
            Platform = platform;
            Configuration = configuration;
            AgentComponents = new FrameworkAgentComponents(configuration, platform, sourceDirectory);
            Version = AgentComponents.Version;
            SourceDirectory = sourceDirectory;
            NugetPushInfo = nugetPushInfo;
        }

        public NugetPushInfo NugetPushInfo { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string SourceDirectory { get; }
        public string Version { get; }
        private FrameworkAgentComponents AgentComponents;
        public string Name => "NugetAzureWebSites";

        public string StagingDirectory => $@"{SourceDirectory}\Build\_staging\{Name}-{Platform}";
        public string PackageDirectory => $@"{SourceDirectory}\Build\Packaging\{Name}";
        private string RootDirectory => $@"{StagingDirectory}\content\newrelic";
        private string ExtensionsDirectory => $@"{StagingDirectory}\content\newrelic\Extensions";
        private string LibDirectory => $@"{StagingDirectory}\lib";
        private string ToolsDirectory => $@"{StagingDirectory}\tools";

        private string NuspecFileName => Platform == "x64" ? "NewRelic.Azure.WebSites.x64.nuspec" : "NewRelic.Azure.WebSites.nuspec";
        private string NuspecFile => $@"{PackageDirectory}\{NuspecFileName}";

        private string OutputDirectory => $@"{SourceDirectory}\Build\BuildArtifacts\{Name}-{Platform}";

        public void Build()
        {
            CheckForMissingComponents();
            CreateStagingDirectory();
            CopyComponents();
            TransformNewRelicConfig();
            TransformNuspecFile();
            Pack();
        }

        private void CheckForMissingComponents()
        {
            var missingComponents = AgentComponents.GetMissingComponents();

            if (missingComponents.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($@"Missing components - make sure you have built the agent for {Platform}-{Configuration}");
                sb.AppendLine();
                foreach (var item in missingComponents)
                {
                    sb.AppendLine(item);
                }
                throw new PackagingException(sb.ToString());
            }
        }

        private void CreateStagingDirectory()
        {
            if (System.IO.Directory.Exists(StagingDirectory))
            {
                System.IO.Directory.Delete(StagingDirectory, true);
            }
            System.IO.Directory.CreateDirectory(StagingDirectory);
            System.IO.Directory.CreateDirectory($@"{StagingDirectory}\content");
            System.IO.Directory.CreateDirectory(RootDirectory);
            System.IO.Directory.CreateDirectory(ExtensionsDirectory);
            System.IO.Directory.CreateDirectory(LibDirectory);
            System.IO.Directory.CreateDirectory(ToolsDirectory);
        }

        private void CopyComponents()
        {
            FileHelpers.CopyFile(AgentComponents.RootInstallDirectoryComponents, RootDirectory);
            FileHelpers.CopyFile(AgentComponents.ExtensionDirectoryComponents, ExtensionsDirectory);
            FileHelpers.CopyFile(AgentComponents.WrapperXmlFiles, ExtensionsDirectory);
            FileHelpers.CopyFile(AgentComponents.AgentApiDll, LibDirectory);
            FileHelpers.CopyFile($@"{PackageDirectory}\tools\install.ps1", ToolsDirectory);
            FileHelpers.CopyFile($@"{PackageDirectory}\tools\uninstall.ps1", ToolsDirectory);
            FileHelpers.CopyFile(NuspecFile, StagingDirectory);
        }

        private void Pack()
        {
            if (Directory.Exists(OutputDirectory))
            {
                Directory.Delete(OutputDirectory, true);
            }

            var nugetPath = File.Exists(@"C:\Nuget.exe") ? @"C:\Nuget.exe" : "nuget";
            var parameters = $@"Pack -NoPackageAnalysis {StagingDirectory}\{NuspecFileName} -OutputDirectory {OutputDirectory}";
            var process = System.Diagnostics.Process.Start(nugetPath, parameters);
            process.WaitForExit(30000);
            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception($"Nuget pack failed complete in timely fashion.");
            }
            if (process.ExitCode != 0)
            {
                throw new Exception($"Nuget pack failed with exit code {process.ExitCode}.");
            }

            if (NugetPushInfo != null)
            {
                var nupkgFile = Directory.EnumerateFiles(OutputDirectory, "*.nupkg").FirstOrDefault();
                parameters = $@"Push {nupkgFile} {NugetPushInfo.ApiKey} -Source {NugetPushInfo.ServerUri}";
                process = System.Diagnostics.Process.Start(nugetPath, parameters);
                process.WaitForExit(30000);
                if (!process.HasExited)
                {
                    process.Kill();
                    throw new Exception($"Nuget push failed complete in timely fashion.");
                }
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Nuget push failed with exit code {process.ExitCode}.");
                }
            }
        }

        private void TransformNewRelicConfig()
        {
            var path = $@"{RootDirectory}\newrelic.config";
            var xml = new System.Xml.XmlDocument();

            // Update the 'newrelic.config' file
            xml.Load(path);
            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "urn:newrelic-config");

            // Remove the 'application' element
            var node = xml.SelectSingleNode("//x:configuration/x:application", ns);
            node.ParentNode.RemoveChild(node);

            // Re-create the 'application' element
            var nodeLog = (System.Xml.XmlElement) xml.SelectSingleNode("//x:configuration/x:log", ns);
            var app = xml.CreateElement("application", "urn:newrelic-config");
            xml.DocumentElement.InsertBefore(app, nodeLog);

            // Set the 'directory' attribute
            nodeLog.SetAttribute("directory", @"c:\Home\LogFiles\NewRelic");
            xml.Save(path);
        }

        private void TransformNuspecFile()
        {
            var path = $@"{StagingDirectory}\{NuspecFileName}";
            var xml = new System.Xml.XmlDocument();
            xml.Load(path);

            var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd");

            var nodeVersion = (System.Xml.XmlElement) xml.SelectSingleNode("//x:package/x:metadata/x:version", ns);
            nodeVersion.InnerText = Version;
            xml.Save(path);
        }
    }
}
