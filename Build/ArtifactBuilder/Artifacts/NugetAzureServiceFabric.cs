using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ArtifactBuilder.Artifacts
{
	public class NugetAzureServiceFabric
	{
		public NugetAzureServiceFabric(string configuration, string sourceDirectory)
		{
			SourceDirectory = sourceDirectory;
			Configuration = configuration;
		}
		private AgentComponents AgentComponents;
		private AgentComponents CoreAgentComponents;
		public string Configuration { get; }
		public string SourceDirectory { get; }
		public string Version { get; set; }
		public string Name => "NugetAzureServiceFabric";
		public string Platform => "x64";
		public string StagingDirectory => $@"{SourceDirectory}\Build\_staging\{Name}";
		public string PackageDirectory => $@"{SourceDirectory}\Build\Packaging\{Name}";
		private string RootDirectory => $@"{StagingDirectory}\content\newrelic";
		private string ExtensionsDirectory => $@"{StagingDirectory}\content\newrelic\extensions";
		private string LibDirectory => $@"{StagingDirectory}\lib";
		private string ToolsDirectory => $@"{StagingDirectory}\tools";
		private string NuspecFileName => $@"NewRelic.Azure.ServiceFabric.nuspec";
		private string NuspecFile => $@"{PackageDirectory}\{NuspecFileName}";
		private string OutputDirectory => $@"{SourceDirectory}\Build\BuildArtifacts\{Name}";

		public void Build()
		{
			AgentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, SourceDirectory);
			CoreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, Configuration, Platform, SourceDirectory);
			AgentComponents.ValidateComponents();
			CoreAgentComponents.ValidateComponents();
			Version = AgentComponents.Version;
			FileHelpers.DeleteDirectories(StagingDirectory, OutputDirectory);
			CopyComponents();

			var newRelicConfigPaths = new[]
			{
				$@"{RootDirectory}\newrelic.config",
				$@"{StagingDirectory}\contentFiles\any\net45\newrelic\newrelic.config",
				$@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic\newrelic.config",
			};
			
			foreach (var newRelicConfigPath in newRelicConfigPaths)
			{
				TransformNewRelicConfig(newRelicConfigPath);
			} 

			TransformNuspecFile();
			NuGetHelpers.Pack($@"{StagingDirectory}\{NuspecFileName}", $"{OutputDirectory}");
		}

		private void CopyComponents()
		{
			FileHelpers.CopyFile(AgentComponents.RootInstallDirectoryComponents, RootDirectory);
			FileHelpers.CopyFile(AgentComponents.ExtensionDirectoryComponents, ExtensionsDirectory);
			FileHelpers.CopyFile(AgentComponents.WrapperXmlFiles, ExtensionsDirectory);
			Directory.CreateDirectory($@"{RootDirectory}\logs");
			System.IO.File.Create($@"{RootDirectory}\logs\placeholder").Dispose();

			FileHelpers.CopyFile(AgentComponents.RootInstallDirectoryComponents, $@"{StagingDirectory}\contentFiles\any\net45\newrelic");
			FileHelpers.CopyFile(AgentComponents.ExtensionDirectoryComponents, $@"{StagingDirectory}\contentFiles\any\net45\newrelic\extensions");
			FileHelpers.CopyFile(AgentComponents.WrapperXmlFiles, $@"{StagingDirectory}\contentFiles\any\net45\newrelic\extensions");
			Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\net45\newrelic\logs");
			System.IO.File.Create($@"{StagingDirectory}\contentFiles\any\net45\newrelic\logs\placeholder").Dispose();

			FileHelpers.CopyFile(CoreAgentComponents.RootInstallDirectoryComponents, $@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic");
			FileHelpers.CopyFile(CoreAgentComponents.ExtensionDirectoryComponents, $@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic\extensions");
			FileHelpers.CopyFile(CoreAgentComponents.WrapperXmlFiles, $@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic\extensions");
			FileHelpers.CopyFile(CoreAgentComponents.LinuxProfiler, $@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic");
			Directory.CreateDirectory($@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic\logs");
			System.IO.File.Create($@"{StagingDirectory}\contentFiles\any\netstandard20\newrelic\logs\placeholder").Dispose();

			FileHelpers.CopyAll($@"{PackageDirectory}\tools", ToolsDirectory);
			FileHelpers.CopyFile(NuspecFile, StagingDirectory);
		}

		private void TransformNewRelicConfig(string newRelicConfigPath)
		{
			var xml = new System.Xml.XmlDocument();

			// Update the 'newrelic.config' file
			xml.Load(newRelicConfigPath);
			var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
			ns.AddNamespace("x", "urn:newrelic-config");

			// Remove the 'application' element
			var node = xml.SelectSingleNode("//x:configuration/x:application", ns);
			node.ParentNode.RemoveChild(node);

			// Re-create the 'application' element
			var nodeLog = (System.Xml.XmlElement)xml.SelectSingleNode("//x:configuration/x:log", ns);
			var app = xml.CreateElement("application", "urn:newrelic-config");
			xml.DocumentElement.InsertBefore(app, nodeLog);

			xml.Save(newRelicConfigPath);
		}

		private void TransformNuspecFile()
		{
			var path = $@"{StagingDirectory}\{NuspecFileName}";
			var xml = new System.Xml.XmlDocument();
			xml.Load(path);

			var ns = new System.Xml.XmlNamespaceManager(xml.NameTable);
			ns.AddNamespace("x", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd");

			var nodeVersion = (System.Xml.XmlElement)xml.SelectSingleNode("//x:package/x:metadata/x:version", ns);
			nodeVersion.InnerText = Version;
			xml.Save(path);
		}
	}
}
