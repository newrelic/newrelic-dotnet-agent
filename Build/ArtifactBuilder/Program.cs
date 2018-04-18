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
					case "nugetazureservicefabric":
						BuildNugetAzureServiceFabric(sourceDirectory, args);
						break;
					case "nugetagentapi":
						BuildNugetAgentApi(sourceDirectory, args);
						break;
					case "nugetazurecloudservices":
						BuildNugetAzureCloudServices(sourceDirectory, args);
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

		private static void BuildNugetAzureServiceFabric(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAzureServiceFabric(configuration, sourceDirectory);
			c.Build();
		}

		private static NugetPushInfo NugetPushInfo = new NugetPushInfo("http://win-nuget-repository.pdx.vm.datanerd.us:81/nuget/Default", "C7B30E3332814310896ADB3DEC35F491");

		private static string GetSourceDirectory()
		{
			var dirInfo = new DirectoryInfo(System.Environment.CurrentDirectory);
			return dirInfo.Parent.FullName;
		}

		private static void BuildNugetAzureWebsites(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var platform = args[2];
			var pushNugetPackage = args.Length == 4;
			var c = new NugetAzureWebSites(platform, configuration, sourceDirectory, pushNugetPackage ? NugetPushInfo : null);
			c.Build();
		}

		private static void BuildNugetAgentApi(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAgentApi(configuration, sourceDirectory, NugetPushInfo);
			c.Build();
		}

		private static void BuildNugetAzureCloudServices(string sourceDirectory, string[] args)
		{
			var configuration = args[1];
			var c = new NugetAzureCloudServices(configuration, sourceDirectory, NugetPushInfo);
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
