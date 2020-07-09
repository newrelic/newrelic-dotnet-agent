using System;
using System.IO;
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
					case "scriptableinstaller":
						BuildScriptableInstaller(sourceDirectory, args);
						break;
					case "nugetazurewebsites":
						BuildNugetAzureWebsites(sourceDirectory, args);
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
		}

		private static void BuildScriptableInstaller(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			new ScriptableInstaller(configuration, sourceDirectory).Build();
		}

	}
}
