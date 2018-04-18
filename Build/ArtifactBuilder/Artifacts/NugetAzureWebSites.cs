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
			SourceDirectory = sourceDirectory;
			NugetPushInfo = nugetPushInfo;
		}

		public NugetPushInfo NugetPushInfo { get; }
		public string Configuration { get; }
		public string Platform { get; }
		public string SourceDirectory { get; }
		public string Version { get; set; }
		private AgentComponents AgentComponents;
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
			AgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, SourceDirectory);
			AgentComponents.ValidateComponents();
			Version = AgentComponents.Version;
			FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
			CopyComponents();
			TransformNewRelicConfig();
			TransformNuspecFile();
			NuGetHelpers.Pack($@"{StagingDirectory}\{NuspecFileName}", $"{OutputDirectory}");
			if (NugetPushInfo != null) NuGetHelpers.Push(NugetPushInfo, $"{OutputDirectory}");
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
