using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArtifactBuilder
{
	public abstract class AgentComponents
	{
		public AgentComponents(string configuration, string platform, string sourcePath)
		{
			Configuration = configuration;
			Platform = platform;
			SourcePath = $@"{sourcePath}\src\Agent";
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
				default:
					throw new Exception("Invalid AgentType");
			}
			agentComponents.CreateAgentComponents();
			return agentComponents;
		}

		public string Platform { get; }
		public string SourcePath { get; }
		public List<string> ExtensionDirectoryComponents { get; set; }
		public List<string> WrapperXmlFiles { get; set; }
		public List<string> RootInstallDirectoryComponents { get; set; }
		public string AgentApiDll;
		public string LinuxProfiler;
		public string ExtensionXsd;
		public string NewRelicXsd;
		public string NewRelicConfig;
		public string NewRelicLicenseFile;
        public string NewRelicThirdPartyNoticesFile;

		private List<string> AllComponents
		{
			get
			{
				var list = RootInstallDirectoryComponents
					.Concat(ExtensionDirectoryComponents)
					.Concat(WrapperXmlFiles)
					.Append(ExtensionXsd)
					.Append(AgentApiDll);

				if (!string.IsNullOrEmpty(LinuxProfiler))
				{
					list = list.Append(LinuxProfiler);
				}

				return list.ToList();
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

		protected abstract string SourceHomeBuilderPath { get; }
		protected abstract List<string> IgnoredHomeBuilderFiles { get; }
		protected abstract void CreateAgentComponents();

		public void CopyComponents(string destinationDirectory)
		{
			FileHelpers.CopyFile(RootInstallDirectoryComponents, destinationDirectory);
			FileHelpers.CopyFile(ExtensionDirectoryComponents, $@"{destinationDirectory}\extensions");
			FileHelpers.CopyFile(WrapperXmlFiles, $@"{destinationDirectory}\extensions");
		}

		public void ValidateComponents()
		{
			CheckForMissingComponents();
			CheckForMissingFilesInHomeBuilderDirectory();
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

			LogAndThrow("Additional Files in Home Builder directory that are missing", missingComponents);
		}
	}
}
