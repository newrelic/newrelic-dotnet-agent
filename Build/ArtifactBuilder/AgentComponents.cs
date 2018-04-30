using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ArtifactBuilder.Artifacts;

namespace ArtifactBuilder
{
	public abstract class AgentComponents
	{
		public AgentComponents(string configuration, string platform, string sourcePath)
		{
			Configuration = configuration;
			Platform = platform;
			SourcePath = $@"{sourcePath}\Agent";
		}

		public string Configuration { get; }

		public static AgentComponents GetAgentComponents(AgentType agentType, string configuration, string platform, string sourceDirectory)
		{
			AgentComponents agentComponents;
			switch (agentType)
			{
				case AgentType.Framework:
					agentComponents = new FrameworkAgentComponents(configuration, platform, sourceDirectory);
					break;
				case AgentType.Core:
					agentComponents = new CoreAgentComponents(configuration, platform, sourceDirectory);
					break;
				default:
					throw new Exception("Invalid AgentType");
			}
			agentComponents.CreateAgentComponents();
			return agentComponents;
		}

		public string Platform { get; }
		public string SourcePath { get; }
		public List<string> ExtensionDirectoryComponents { get; set; }
		public List<string> NetstandardExtensionDirectoryComponents { get; set; }
		public List<string> WrapperXmlFiles { get; set; }
		public List<string> RootInstallDirectoryComponents { get; set; }
		public string AgentApiDll;
		public string LinuxProfiler;
		public string ExtensionXsd;
		public string NewRelicXsd;
		public string NewRelicConfig;
		
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

		protected abstract void CreateAgentComponents();

		public void CopyComponents(string destinationDirectory)
		{
			FileHelpers.CopyFile(RootInstallDirectoryComponents, destinationDirectory);
			FileHelpers.CopyFile(ExtensionDirectoryComponents, $@"{destinationDirectory}\extensions");
			FileHelpers.CopyFile(NetstandardExtensionDirectoryComponents, $@"{destinationDirectory}\extensions\netstandard2.0");
			FileHelpers.CopyFile(WrapperXmlFiles, $@"{destinationDirectory}\extensions");
		}

		public void ValidateComponents()
		{
			var missingComponents = GetMissingComponents();

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

			var xmlFilesNotUtf8 = GetXmlFilesThatAreNotUtf8();

			if (xmlFilesNotUtf8.Count > 0)
			{
				var sb = new System.Text.StringBuilder();
				sb.AppendLine($@"xml/xsd files that ship with the agent must be UTF-8 (no BOM) - the following files are either not UTF-8 or contain a BOM");
				sb.AppendLine();
				foreach (var item in xmlFilesNotUtf8)
				{
					sb.AppendLine(item);
				}
				throw new PackagingException(sb.ToString());
			}
		}

		private List<string> GetMissingComponents()
		{
			var missingComponents = new List<string>();

			foreach (var item in RootInstallDirectoryComponents)
			{
				if (!File.Exists(item)) missingComponents.Add(item);
			}

			foreach (var item in ExtensionDirectoryComponents)
			{
				if (!File.Exists(item)) missingComponents.Add(item);
			}

			foreach (var item in NetstandardExtensionDirectoryComponents)
			{
				if (!File.Exists(item)) missingComponents.Add(item);
			}

			foreach (var item in WrapperXmlFiles)
			{
				if (!File.Exists(item)) missingComponents.Add(item);
			}

			if (!File.Exists(AgentApiDll)) missingComponents.Add(AgentApiDll);
			if (!File.Exists(ExtensionXsd)) missingComponents.Add(ExtensionXsd);
			if (!string.IsNullOrEmpty(LinuxProfiler) && !File.Exists(LinuxProfiler)) missingComponents.Add(LinuxProfiler);

			return missingComponents;
		}

		private List<string> GetXmlFilesThatAreNotUtf8()
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

			return nonUtf8Files;
		}
	}
}
