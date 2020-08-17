// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;

namespace PlatformTests.Applications
{
    public class AwsLambdaApplication : BaseApplication
    {
        private const string TestPackageName = "NewRelic.OpenTracing.AmazonLambda.Tracer";
        private const string SolutionName = "AwsLambdaTestApplication";
        private string FunctionName;
        private const string LogGroupNamePrefix = "/aws/lambda";

        public string ApplicationRootDirectory { get; }
        public string ApplicationOutputPath { get; }
        public string SolutionConfiguration { get; }
        private string _apiGatewayEndpoint;
        public string LogGroupName { get; private set; }


        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }

        public AwsLambdaApplication(string applicationName, string testSettingCategory) : base(applicationName, null, testSettingCategory)
        {
            var apiGatewayResouceId = TestConfiguration["ApplicationGatewayResourceId"];

            _apiGatewayEndpoint = $"https://{apiGatewayResouceId}.execute-api.us-west-2.amazonaws.com/test/{ApplicationName}";
            ApplicationRootDirectory = Path.GetFullPath(Path.Combine(RootRepositoryPath, $@"tests\Agent\PlatformTests\Applications\{SolutionName}"));

            SolutionConfiguration = "Release";
#if DEBUG
			SolutionConfiguration = "Debug";
#endif
            FunctionName = ApplicationName;
            ApplicationOutputPath = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}\bin\{SolutionConfiguration}");

            LogGroupName = LogGroupNamePrefix + "/" + ApplicationName;
        }

        private static string TestPackageLocalPath { get; } = Path.GetFullPath(Path.Combine(RootRepositoryPath, @"build\BuildArtifacts\NugetAwsLambdaOpenTracer\"));

        public override string[] NugetSources { get; } =
        {
            TestPackageLocalPath,
            "https://api.nuget.org/v3/index.json"
        };

        public override void BuildAndDeploy()
        {
            try
            {

                var workingDirectory = Path.Combine(ApplicationRootDirectory, ApplicationName);
                FunctionName = ApplicationName;

                var functionRole = TestConfiguration.DefaultSetting.AwsTestRoleArn;

                var envString = $@"NEW_RELIC_ACCOUNT_ID={TestConfiguration.DefaultSetting.NewRelicAccountId};NEW_RELIC_DEBUG_MODE=true;NEW_RELIC_TRUSTED_ACCOUNT_KEY={TestConfiguration.DefaultSetting.NewRelicAccountId}";

                var arguments = $@"lambda deploy-function {FunctionName} --function-role {functionRole} --project-location {workingDirectory} --environment-variables {envString}";
                InvokeAnExecutable("dotnet.exe", arguments, workingDirectory);
            }
            catch
            {
                TestLogger?.WriteLine($@"[{DateTime.Now}] FAILED TO DEPLOY .");
            }
        }

        public override void InstallAgent()
        {
            var version = GetNugetPackageVersion(TestPackageLocalPath);

            TestLogger?.WriteLine($@"[{DateTime.Now}] Installing {TestPackageName} version {version} .");

            UpdatePackageReference(TestPackageName, version);
            RestoreNuGetPackage(NugetSources);

            TestLogger?.WriteLine($@"[{DateTime.Now}] {TestPackageName} version {version} installed.");
        }

        public override void StopTestApplicationService()
        {
            // noop
        }

        public string ExerciseFunction(Dictionary<string, string> headers = null, string queryStringParameters = null)
        {
            StartTime = DateTime.UtcNow;

            var headerCollection = new WebHeaderCollection();

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    headerCollection.Add(header.Key, header.Value);
                }
            }

            var response = string.Empty;
            using (var client = new WebClient())
            {
                if (headerCollection.Count > 0)
                {
                    client.Headers = headerCollection;
                }

                var address = string.IsNullOrEmpty(queryStringParameters) ? _apiGatewayEndpoint : _apiGatewayEndpoint + "?" + queryStringParameters;
                response = client.DownloadString(address);

            }

            EndTime = DateTime.UtcNow;

            return response;
        }

        private void UpdatePackageReference(string packageName, string version)
        {
            var projectFile = Path.Combine(ApplicationRootDirectory, $@"{ApplicationName}\{ApplicationName}.csproj");

            var xml = new XmlDocument();
            var nsmgr = new XmlNamespaceManager(xml.NameTable);

            xml.Load(projectFile);

            var packageReferenceNode = xml.SelectSingleNode($@"//PackageReference[@Include='{packageName}']", nsmgr);

            if (packageReferenceNode?.Attributes != null) packageReferenceNode.Attributes["Version"].Value = version;

            xml.Save(projectFile);
        }

        private string GetNugetPackageVersion(string nugetSource)
        {
            var package = Directory.GetFiles(nugetSource).FirstOrDefault();
            if (package != null)
            {
                var parts = package.Split('.');

                return $@"{parts[parts.Length - 5]}.{parts[parts.Length - 4]}.{parts[parts.Length - 3]}";

            }

            return string.Empty;
        }

        private void RestoreNuGetPackage(string[] sources)
        {
            TestLogger?.WriteLine($@"[{DateTime.Now}] Restoring NuGet packages.");

            var solutionFile = Path.Combine(ApplicationRootDirectory, $@"{SolutionName}.sln");
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
    }
}
