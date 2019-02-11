using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using NewRelic.Agent.IntegrationTestHelpers;

namespace PlatformTests.Applications
{
	public class ServiceFabricApplication : BaseApplication
	{
		public String ApplicationRootDirectory { get; }
		 
		public String ApplicationPackagePath { get; }

		public String SolutionConfiguration { get; } 

		public ServiceFabricApplication(string applicationName, string[] serviceNames):base(applicationName, serviceNames)
		{
			ApplicationRootDirectory = Path.GetFullPath(Path.Combine(RootRepositoryPath, $@"Tests\PlatformTests\Applications\{ApplicationName}"));

			SolutionConfiguration = "Release";
#if DEBUG
			SolutionConfiguration = "Debug";
#endif

			ApplicationPackagePath = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}\pkg\{SolutionConfiguration}");
		}

		public override string[] NugetSources { get; } =
		{
			Path.GetFullPath(Path.Combine(RootRepositoryPath, @"Build\BuildArtifacts\NugetAgent\")),
			"http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default",
			"https://api.nuget.org/v3/index.json"
		};

		public override void InstallAgent()
		{
			var packageName = "NewRelic.Agent";

			var version = SearchForNewestNugetVersion(Path.GetFullPath(Path.Combine(RootRepositoryPath, @"Build\BuildArtifacts\NugetAgent\")));

			TestLogger?.WriteLine($@"[{DateTime.Now}] Installing {packageName} version {version} .");

			UpdatePackageReference(packageName, version);

			TestLogger?.WriteLine($@"[{DateTime.Now}] {packageName} version {version} installed.");

			RestoreNuGetPackage(NugetSources);
		}

		private string SearchForNewestNugetVersion(string nugetSource)
		{
			List<string> packages = new List<string>();

			foreach (var file in Directory.GetFiles(nugetSource))
			{
				packages.Add(Path.GetFileNameWithoutExtension(file));
			}

			var package =  packages.First();
			var parts = package.Split('.');

			return $@"{parts[parts.Length - 3]}.{parts[parts.Length - 2]}.{parts[parts.Length - 1]}";
		}

		private void UpdatePackageReference(string packageName, string version)
		{
			foreach (var serviceName in ServiceNames)
			{
				var projectFile = Path.Combine(ApplicationRootDirectory, $@"{serviceName}\{serviceName}.csproj");

				const string ns = "http://schemas.microsoft.com/developer/msbuild/2003";
				var xml = new XmlDocument();
				var nsmgr = new XmlNamespaceManager(xml.NameTable);
				nsmgr.AddNamespace("msbld", ns);

				xml.Load(projectFile);

				var packageReferenceNode = xml.SelectSingleNode($@"//msbld:PackageReference[@Include='{packageName}']", nsmgr);

				if (packageReferenceNode?.Attributes != null) packageReferenceNode.Attributes["Version"].Value = version;

				xml.Save(projectFile);
			}
		}

		private void RestoreNuGetPackage(string[] sources)
		{
			TestLogger?.WriteLine($@"[{DateTime.Now}] Restoring NuGet packages.");

			var solutionFile = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}.sln");
			var sourceArgument = String.Join(";", sources);

			var arguments = $@"restore {solutionFile} -Source ""{sourceArgument}"" -NoCache -NonInteractive";

			try
			{
				InvokeAnExecutable(NugetPath, arguments, ApplicationRootDirectory);
			}
			catch (Exception ex)
			{
				throw new Exception($@"There were errors while restoring nuget packages for {solutionFile}: {ex.Message}");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] Nuget packages restored.");
		}

		private void BuildAndPackage()
		{
			TestLogger?.WriteLine($@"[{DateTime.Now}] Building and packaging the test application.");

			var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
			var arguments = $@"{ApplicationName}.sfproj /t:Package /p:Configuration={SolutionConfiguration}";
			try
			{
				InvokeAnExecutable(MsbuildPath, arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while packaging the test application");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] Finished.");
		}

		public override void BuildAndDeploy()
		{
			BuildAndPackage();

			UpdateNewRelicConfig();

			TryToInstallCertificate();

			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Deploying");

			var arguments = $@".\Scripts\Deploy-FabricApplication.ps1 -PublishProfileFile .\PublishProfiles\Cloud.xml -ApplicationPackagePath .\pkg\{SolutionConfiguration} -OverwriteBehavior Always";
			var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);

			try
			{
				InvokeAnExecutable("powershell.exe", arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while deploying the test application");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Deployed");

		}

		public override void StopTestApplicationService()
		{
			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Undeploying");

			var arguments = $@".\Scripts\Undeploy-FabricApplication.ps1 -ApplicationName fabric:/{ApplicationName} -PublishProfileFile .\PublishProfiles\Cloud.xml";
			var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);

			try
			{
				InvokeAnExecutable("powershell.exe", arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while undeploying the test application");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Undeployed");
		}

		private void TryToInstallCertificate()
		{
			X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
			store.Open(OpenFlags.ReadOnly);

			var certificates = store.Certificates.Find(
				X509FindType.FindByThumbprint,
				"2e60de9290aec6f15eae9b5e24b7c6d1eeb590cb",
				false);

			if (certificates.Count > 0)
			{
				store.Close();
				return;
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] dotnet-sf-cert.pfx certificate is not installed. Installing one.");

			string certFile = Path.Combine(ApplicationRootDirectory, "dotnet-sf-cert.pfx");// Contains name of certificate file

			var cert = new X509Certificate2(certFile, "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

			store.Open(OpenFlags.ReadWrite);

			store.Add(cert);
			store.Close();
		
		}

		private void UpdateNewRelicConfig()
		{
			foreach (var serviceName in ServiceNames)
			{
				var newRelicConfigPath = Path.Combine(ApplicationPackagePath, $@"{serviceName}Pkg/Code/newrelic/newrelic.config");
				var newRelicConfigModifier = new NewRelicConfigModifier(newRelicConfigPath);
				newRelicConfigModifier.ForceTransactionTraces();
				newRelicConfigModifier.SetLogLevel("debug");
			}
		}
	}
}
