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
		public string Configuration { get; }
		public string Platform { get; }
		public string MsiDirectory { get; }

		private readonly List<string> _extensionFilesToIgnore = new List<string>
		{
			"extension.xsd"
		};

		public MsiInstaller(string sourceDirectory, string platform, string configuration) : base(sourceDirectory, "MsiInstaller")
		{
			Platform = platform;
			Configuration = configuration;
			MsiDirectory = $@"{sourceDirectory}\src\Agent\_build\{Platform}-{Configuration}\Installer";
			OutputDirectory = $@"{SourceDirectory}\build\BuildArtifacts\{Name}-{Platform}";
		}

		protected override void InternalBuild()
		{
			if (!Directory.Exists(MsiDirectory))
			{
				Console.WriteLine("Warning: The {0} directory does not exist.", MsiDirectory);
				return;
			}

			ValidateWxsDefinitionFileForInstaller();

			var fileSearchPattern = $@"NewRelicAgent_{Platform}_*.msi";
			var msiPath = Directory.GetFiles(MsiDirectory, fileSearchPattern).FirstOrDefault();

			if (string.IsNullOrEmpty(msiPath))
			{
				Console.WriteLine("Warning: The {0} installer could not be found.", fileSearchPattern);
				return;
			}

			FileHelpers.CopyFile(msiPath, OutputDirectory);
			File.WriteAllText($@"{OutputDirectory}\checksum.sha256" , FileHelpers.GetSha256Checksum(msiPath));
		}

		private void ValidateWxsDefinitionFileForInstaller()
		{
			//Verify that the expected agent components are in the homebuilder for the installer
			var agentComponents = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, Platform, SourceDirectory);
			agentComponents.ValidateComponents();

			var productWxs = GetParsedProductWxsData();

			var extensionGroup = productWxs.Fragment.Items
				.OfType<WixFragmentComponentGroup>()
				.First(cg => cg.Id == "NewRelic.Agent.Extensions");
			var instrumentationGroup = productWxs.Fragment.Items
				.OfType<WixFragmentComponentGroup>()
				.First(cg => cg.Id == "NewRelic.Agent.Extensions.Instrumentation");

			ValidateWixFileExtensionDefinitions(extensionGroup);
			ValidateWixFileExtensionDefinitions(instrumentationGroup);

			ValidateAgentComponentsAndWixReferenceTheSameFiles(agentComponents.ExtensionDirectoryComponents, extensionGroup);
			ValidateAgentComponentsAndWixReferenceTheSameFiles(agentComponents.WrapperXmlFiles, instrumentationGroup);
		}

		private Wix GetParsedProductWxsData()
		{
			using (var xmlReader = XmlReader.Create($@"{SourceDirectory}\src\Agent\Installer\Product.wxs"))
			{
				var serializer = new XmlSerializer(typeof(Wix));
				return (Wix)serializer.Deserialize(xmlReader);
			}
		}

		private void ValidateWixFileExtensionDefinitions(WixFragmentComponentGroup group)
		{
			foreach (var component in group.Component)
			{
				var file = component.File;
				if (file.KeyPath != "yes")
				{
					throw new PackagingException($"Product.wxs file {file.Id} did not have KeyPath set to yes, but was {file.KeyPath}.");
				}

				var expectedSourcePath = $@"$(var.SolutionDir)New Relic Home $(var.Platform)\Extensions\{file.Name}";
				if (file.Source != expectedSourcePath)
				{
					throw new PackagingException($"Product.wxs file {file.Id} did not have the expected source path of {expectedSourcePath}, but was {file.Source}");
				}
			}
		}

		private void ValidateAgentComponentsAndWixReferenceTheSameFiles(IList<string> agentComponent, WixFragmentComponentGroup wixGroup)
		{
			var agentComponentFiles = agentComponent.Select(Path.GetFileName).Except(_extensionFilesToIgnore).ToList();
			var wixFiles = wixGroup.Component.Select(c => c.File.Name).ToList();

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
	}
}
