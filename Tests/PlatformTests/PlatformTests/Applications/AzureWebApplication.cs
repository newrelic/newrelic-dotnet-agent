using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;

namespace PlatformTests.Applications
{
	public class AzureWebApplication : BaseApplication
	{
		private const string NewRelicLogFilesUri = @"ftp://waws-prod-mwh-001.ftp.azurewebsites.windows.net/LogFiles/NewRelic";
		private const string UserName = "dotnet-agent";
		private const string Password = "**REDACTED**";
		private const string PublishSettings = "dotnet-agent.publishsettings";
		private const string TestPackageName = "NewRelic.Azure.WebSites.x64";

		private static readonly string TestPackageLocalPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
			@"..\..\..\..\..\Build\BuildArtifacts\NugetAzureWebSites-x64\"));

		public String ApplicationRootDirectory { get; }

		public String ApplicationOutputPath { get; }

		public String SolutionConfiguration { get; }

		public AzureWebApplication(string applicationName) : base(applicationName, null)
		{
			ApplicationRootDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
				$@"..\..\..\Applications\{ApplicationName}"));

			SolutionConfiguration = "Release";
#if DEBUG
			SolutionConfiguration = "Debug";
#endif

			ApplicationOutputPath = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}\bin\{SolutionConfiguration}");
		}

		public override string[] NugetSources { get; } =
		{
			TestPackageLocalPath,
			"http://win-nuget-repository.pdx.vm.datanerd.us:81/NuGet/Default",
			"https://api.nuget.org/v3/index.json"
		};

		public override void InstallAgent()
		{
			var version = GetNugetPackageVersion(TestPackageLocalPath);

			TestLogger?.WriteLine($@"[{DateTime.Now}] Installing {TestPackageName} version {version} .");

			RestoreNuGetPackage(NugetSources);

			UpdateNewRelicAgentNuGetPackage(TestPackageName, version, NugetSources);

			TestLogger?.WriteLine($@"[{DateTime.Now}] {TestPackageName} version {version} installed.");
		}

		private string GetNugetPackageVersion(string nugetSource)
		{
			var package = Directory.GetFiles(nugetSource).FirstOrDefault();
			if (package != null)
			{
				var parts = package.Split('.');

				return $@"{parts[parts.Length - 4]}.{parts[parts.Length - 3]}.{parts[parts.Length - 2]}";
			}

			return String.Empty;
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
			catch
			{
				throw new Exception($@"There were errors while restoring nuget packages for {solutionFile}");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] Nuget packages restored.");
		}

		private void UpdateNewRelicAgentNuGetPackage(string packageName, string version, string[] sources)
		{
			TestLogger?.WriteLine($@"[{DateTime.Now}] Restoring NuGet packages.");

			var solutionFile = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}.sln");
			var sourceArgument = String.Join(";", sources);

			var arguments = $@"update {solutionFile} -Id {packageName} -Version {version} -Source ""{sourceArgument}"" -FileConflictAction overwrite -NonInteractive";

			try
			{
				InvokeAnExecutable(NugetPath, arguments, ApplicationRootDirectory);
			}
			catch
			{
				throw new Exception($@"There were errors while updating nuget packages for {solutionFile}");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] Nuget packages updated.");
		}

		public override void BuildAndDeploy()
		{
			//Stopping app serice
			TestLogger?.WriteLine($@"[{DateTime.Now}] Stopping the application service.");

			var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
			var arguments = $@"& .\DeployUtils\StopAppService.ps1 -appName NetFrameworkBasicApplication -publishSettings .\{PublishSettings}";
			try
			{
				InvokeAnExecutable("powershell.exe", arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while packaging the test application");
			}

			//Clean up NewRelic log folder on the server
			Thread.Sleep(5000);
			DeleteNewRelicLogFiles();

			//Building and deploying app service
			TestLogger?.WriteLine($@"[{DateTime.Now}] Building and deploying the test application.");

			workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
			arguments = $@"{ApplicationName}.csproj /p:DeployOnBuild=true /p:PublishProfile=DeployProfile /p:Configuration={SolutionConfiguration}  /p:username={UserName} /p:Password={Password} ";
			try
			{
				InvokeAnExecutable(MsbuildPath, arguments, workingDirectory);
			}
			catch (Exception ex)
			{
				throw new Exception($"There were errors while packaging the test application: {ex.Message}");
			}

			TestLogger?.WriteLine($@"[{DateTime.Now}] Finished.");

			//Starting app serice
			TestLogger?.WriteLine($@"[{DateTime.Now}] Starting the application service.");

			workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
			arguments = $@"& .\DeployUtils\StartAppService.ps1 -appName {ApplicationName} -publishSettings .\{PublishSettings}";
			try
			{
				InvokeAnExecutable("powershell.exe", arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while packaging the test application");
			}
		}


		public override void StopTestApplicationService()
		{
			//Stopping app serice
			TestLogger?.WriteLine($@"[{DateTime.Now}] Stopping the application service.");

			var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
			var arguments = $@"& .\DeployUtils\StopAppService.ps1 -appName {ApplicationName} -publishSettings .\{PublishSettings}";
			try
			{
				InvokeAnExecutable("powershell.exe", arguments, workingDirectory);
			}
			catch
			{
				throw new Exception("There were errors while packaging the test application");
			}
		}

		private void DeleteNewRelicLogFiles()
		{
			var credentials = new NetworkCredential($@"{ApplicationName}\{UserName}", Password);

			var filesToDelete = GetFiles(NewRelicLogFilesUri, credentials);

			foreach (var fileName in filesToDelete)
			{
				var reqFtp = WebRequest.Create(NewRelicLogFilesUri + $@"/{fileName}");
				reqFtp.Method = WebRequestMethods.Ftp.DeleteFile;
				reqFtp.Credentials = credentials;

				using (reqFtp.GetResponse())
				{

				}
			}
		}

		private IEnumerable GetFiles(string uri, ICredentials credentials)
		{
			var reqFtp = WebRequest.Create(uri);
			reqFtp.Method = WebRequestMethods.Ftp.ListDirectory;
			reqFtp.Credentials = credentials;

			var files = new List<String>();

			using (var response = reqFtp.GetResponse())
			{
				var responseStream = response.GetResponseStream();
				using (var reader = new StreamReader(responseStream))
				{
					string line;
					while ((line = reader.ReadLine()) != null)
					{
						files.Add(line);
					}
				}
			}

			return files;
		}
	}
}

