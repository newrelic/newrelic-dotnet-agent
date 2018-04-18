using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder.Artifacts
{
	public class ScriptableInstaller : Artifact
	{
		public ScriptableInstaller(string configuration, string sourceDirectory)
			: base(sourceDirectory, nameof(ScriptableInstaller))
		{
			Configuration = configuration;
		}

		public string Configuration { get; }

		protected override void InternalBuild()
		{
			var x64Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x64", SourceDirectory);
			var x86Components = AgentComponents.GetAgentComponents(AgentType.Framework, Configuration, "x86", SourceDirectory);
			x64Components.ValidateComponents();
			x86Components.ValidateComponents();

			FileHelpers.CopyAll($@"{PackageDirectory}\Installer", $@"{StagingDirectory}");
			var replacements = new Dictionary<string, string>() { { "AGENT_VERSION_STRING", x64Components.Version } };
			FileHelpers.ReplaceTextInFile($@"{StagingDirectory}\install.ps1", replacements);

			CreateNugetPackage(x64Components, x86Components, $@"{PackageDirectory}\NewRelic.Net.Agent.x64.nuspec");
			CreateNugetPackage(x86Components, x86Components, $@"{PackageDirectory}\NewRelic.Net.Agent.nuspec");

			var zipFilePath = $@"{OutputDirectory}\NewRelic.Agent.Installer.{x64Components.Version}.zip";
			Directory.CreateDirectory(OutputDirectory);
			System.IO.Compression.ZipFile.CreateFromDirectory(StagingDirectory, zipFilePath);
		}

		private void CreateNugetPackage(AgentComponents components, AgentComponents x86Components, string nuspecPath)
		{
			var rootDir = $@"{StagingDirectory}\Nuget{components.Platform}";
			var stagingDir = $@"{rootDir}\content\newrelic";
			FileHelpers.CopyFile(nuspecPath, rootDir);

			var package = new NugetPackage(rootDir, $@"{StagingDirectory}", null);
			package.SetVersion(components.Version);
			var configFilePath = $@"{rootDir}\content\newrelic\newrelic.config";
			FileHelpers.CopyFile(components.RootInstallDirectoryComponents, stagingDir);
			FileHelpers.CopyFile(components.RootInstallDirectoryComponents.Where(x => !x.Contains("newrelic.config") && !x.Contains("newrelic.xsd")), $@"{stagingDir}\ProgramFiles\NewRelic\NetAgent");
			FileHelpers.CopyFile(components.ExtensionDirectoryComponents.Where(x => x.Contains(".dll")), $@"{stagingDir}\ProgramFiles\NewRelic\NetAgent\Extensions");
			FileHelpers.CopyFile(x86Components.RootInstallDirectoryComponents.Where(x => x.Contains("NewRelic.Profiler.dll")), $@"{stagingDir}\ProgramFiles\NewRelic\NetAgent\x86");
			FileHelpers.CopyFile(components.WrapperXmlFiles, $@"{stagingDir}\ProgramData\NewRelic\NetAgent\Extensions");
			FileHelpers.CopyFile(components.ExtensionXsd, $@"{stagingDir}\ProgramData\NewRelic\NetAgent\Extensions");
			FileHelpers.CopyFile(components.NewRelicXsd, $@"{stagingDir}\ProgramData\NewRelic\NetAgent");
			FileHelpers.CopyFile(configFilePath, $@"{stagingDir}\ProgramData\NewRelic\NetAgent");
			Directory.CreateDirectory($@"{stagingDir}\Extensions");
			Directory.CreateDirectory($@"{stagingDir}\ProgramData\NewRelic\NetAgent\NewRelic\NetAgent\Extensions");
			Directory.CreateDirectory($@"{stagingDir}\ProgramData\NewRelic\NetAgent\NewRelic\NetAgent\Logs");
			File.Delete(configFilePath);
			package.Pack();
			Directory.Delete(rootDir, true);
		}
	}
}
