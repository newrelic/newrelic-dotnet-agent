using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArtifactBuilder.Artifacts;

namespace ArtifactBuilder
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				var sourceDirectory = GetSourceDirectory();
				var package = args[0].ToLower();

				switch (package)
				{
					case "ziparchives":
						BuildZipArchives(sourceDirectory, args);
						break;
					case "coreinstaller":
						BuildCoreInstaller(sourceDirectory, args);
						break;
					case "scriptableinstaller":
						BuildScriptableInstaller(sourceDirectory, args);
						break;
					case "nugetazurewebsites":
						BuildNugetAzureWebsites(sourceDirectory, args);
						break;
					case "azuresiteextension":
						BuildAzureSiteExtension(sourceDirectory, args);
						break;
					case "nugetagent":
						BuildNugetAgent(sourceDirectory, args);
						break;
					case "nugetagentapi":
						BuildNugetAgentApi(sourceDirectory, args);
						break;
					case "nugetazurecloudservices":
						BuildNugetAzureCloudServices(sourceDirectory, args);
						break;
					case "msiinstaller":
						BuildMsiInstaller(sourceDirectory, args);
						break;
					case "downloadsite":
						BuildDownloadSite(sourceDirectory, args);
						break;
					case "linuxpackages":
						BuildLinuxPackages(sourceDirectory);
						break;
					default:
						throw new Exception($"Unknown package type: {args[0]}");
				}
			}
			catch (PackagingException e)
			{
				Console.Error.WriteLine(e.Message);
				return 1;
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				return 1;
			}

			return 0;
		}

		private static void BuildLinuxPackages(string sourceDirectory)
		{
			new LinuxPackage(sourceDirectory, "LinuxDeb", "_amd64", "deb").Build();
			new LinuxPackage(sourceDirectory, "LinuxRpm", ".x86_64", "rpm").Build();
			new LinuxPackage(sourceDirectory, "LinuxTar", "_amd64", "tar.gz").Build();
		}

		private static void BuildDownloadSite(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new DowloadSiteArtifact(sourceDirectory, configuration).Build();
		}

		private static void BuildMsiInstaller(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new MsiInstaller(sourceDirectory, "x86", configuration).Build();
			new MsiInstaller(sourceDirectory, "x64", configuration).Build();
		}

		private static void BuildNugetAgent(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAgent(configuration, sourceDirectory);
			c.Build();
		}

		private static string GetSourceDirectory()
		{
			var dirInfo = new DirectoryInfo(System.Environment.CurrentDirectory);
			return dirInfo.Parent.FullName;
		}

		private static void BuildNugetAzureWebsites(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var platform = args[2];
			var c = new NugetAzureWebSites(platform, configuration, sourceDirectory);
			c.Build();
		}

		private static void BuildNugetAgentApi(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAgentApi(configuration, sourceDirectory);
			c.Build();
		}

		private static void BuildNugetAzureCloudServices(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAzureCloudServices(configuration, sourceDirectory);
			c.Build();
		}

		private static void BuildZipArchives(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new ZipArchive(AgentType.Framework, "x64", configuration, sourceDirectory).Build();
			new ZipArchive(AgentType.Framework, "x86", configuration, sourceDirectory).Build();
			new ZipArchive(AgentType.Core, "x64", configuration, sourceDirectory).Build();
			new ZipArchive(AgentType.Core, "x86", configuration, sourceDirectory).Build();
		}

		private static void BuildCoreInstaller(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new CoreInstaller(configuration, sourceDirectory).Build();
		}

		private static void BuildScriptableInstaller(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new ScriptableInstaller(configuration, sourceDirectory).Build();
		}

		private static void BuildAzureSiteExtension(string sourceDirectory, string[] args)
		{
			var version = args[1];
			var c = new AzureSiteExtension(version, sourceDirectory);
			c.Build();
		}
	}
}
