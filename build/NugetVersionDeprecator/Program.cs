// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using CommandLine;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Octokit;
using RestSharp;
using Repository = NuGet.Protocol.Core.Types.Repository;

namespace NugetVersionDeprecator;

internal class Program
{
    private const string RepoUrl = "https://api.nuget.org/v3/index.json";
    private const string NewRelicUrl = "https://api.newrelic.com/graphql";

    private const string NerdGraphQueryJson = "{ \"query\": \"{ docs { agentReleases(agentName: DOTNET) { version eolDate } } }\" }";


    static async Task<int> Main(string[] args)
    {
        try
        {
            var options = Parser.Default.ParseArguments<Options>(args)
                .WithParsed(ValidateOptions)
                .WithNotParsed(HandleParseError)
                .Value;

            if (options.TestMode)
                Console.WriteLine("**** TEST MODE *** No Github Issues will be created.");

            var configuration = LoadConfiguration(options.ConfigurationPath);

            var nerdGraphResponse = await QueryNerdGraphAsync(options.ApiKey, NewRelicUrl);

            var agentReleases = ParseNerdGraphResponse(DateTime.UtcNow,  nerdGraphResponse);
            if (agentReleases.Any())
            {
                List<PackageDeprecationInfo> packagesToDeprecate = new();

                foreach (var package in configuration.Packages)
                {
                    packagesToDeprecate.AddRange(await GetPackagesToDeprecateAsync(package, agentReleases));
                }

                if (packagesToDeprecate.Any())
                {
                    var message = ReportPackagesToDeprecate(packagesToDeprecate, agentReleases);
                    Console.WriteLine(message);

                    if (!options.TestMode)
                        await CreateGhIssueAsync(message, options.GithubToken);
                }
            }
            else
            {
                Console.WriteLine("No eligible deprecated Agent released found.");
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 1;
        }
    }

    private static async Task<string> QueryNerdGraphAsync(string apiKey, string url)
    {
        // Send a NerdGraph query to get a list of all .NET Agent versions
        RestResponse response;
        using var client = new RestClient();

        var request = new RestRequest(url, Method.Post);
        request.AddStringBody(NerdGraphQueryJson, DataFormat.Json);
        request.AddHeader("API-Key", apiKey);

        try
        {
            response = await client.PostAsync(request);
        }
        catch (Exception e)
        {
            throw new Exception("NerdGraph query failed.", e);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"NerdGraph query failed: {response}");
        }

        return response.Content;
    }

    private static List<AgentRelease> ParseNerdGraphResponse(DateTime releaseDate, string nerdGraphResponse)
    {
        // parse the NerdGraph response -- we want to deserialize data.docs.agentReleases into an array of AgentRelease
        var parsedResponse = JObject.Parse(nerdGraphResponse);
        var allAgentReleases = parsedResponse.SelectToken("data.docs.agentReleases", false)?.ToObject<List<AgentRelease>>();
        if (allAgentReleases == null)
        {
            throw new Exception($"Unable to parse NerdGraph response: {Environment.NewLine}{nerdGraphResponse}");
        }

        var deprecatedReleases = allAgentReleases.Where(ar => ar.EolDate <= releaseDate).ToList();

        return deprecatedReleases;
    }

    private static string ReportPackagesToDeprecate(List<PackageDeprecationInfo> packagesToDeprecate, List<AgentRelease> agentReleases)
    {
        var sb = new StringBuilder();

        sb.AppendLine("The following NuGet packages should be deprecated:");
        foreach (var package in packagesToDeprecate)
        {
            var eolRelease = agentReleases.Single(ar => ar.Version.StartsWith(package.PackageVersion));

            sb.AppendLine($"  * {package.PackageName} v{package.PackageVersion} (EOL as of {eolRelease.EolDate.ToShortDateString()})");
        }

        return sb.ToString();
    }

    static async Task<IEnumerable<PackageDeprecationInfo>> GetPackagesToDeprecateAsync(string packageName, List<AgentRelease> agentReleases)
    {
        // query NuGet for a current list of non-deprecated versions of all .NET Agent packages
        SourceCacheContext cache = new SourceCacheContext();
        SourceRepository repository = Repository.Factory.GetCoreV3(RepoUrl);
        PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();
        var packages = (await resource.GetMetadataAsync(
                packageName,
                includePrerelease: false,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                CancellationToken.None)).Cast<PackageSearchMetadata>()
            // because of how NuGet query works, we'll get a whole lot of "close" match results - so we have to narrow the response down
            // to only those packages where the name matches packageName
            .Where(p =>
                p.DeprecationMetadata is null // not deprecated
                && string.Equals(p.Identity.Id, packageName, StringComparison.CurrentCultureIgnoreCase)).ToList();

        // get the nuget packages with versions matching the list of all agent releases
        var currentVersions = agentReleases.Select(ar => NuGetVersion.Parse(ar.Version)).ToList();
        var packagesToDeprecate = packages.Where(p => currentVersions.Contains(p.Version)).ToList();

        return packagesToDeprecate.Select(p =>
            new PackageDeprecationInfo() { PackageName = p.PackageId, PackageVersion = p.Version.ToString() });
    }

    static async Task CreateGhIssueAsync(string message, string githubToken)
    {
        var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("NugetVersionDeprecator"));
        var tokenAuth = new Credentials(githubToken);
        ghClient.Credentials = tokenAuth;


        var newIssue = new NewIssue($"chore(NugetDeprecator): Deprecate Nuget packages.")
        {
            Body = message
        };

        newIssue.Labels.Add("Deprecation");
        newIssue.Labels.Add("Nuget");

        var issue = await ghClient.Issue.Create("newrelic", "newrelic-dotnet-agent", newIssue);

        Console.WriteLine($"Created new GitHub Issue #{issue.Number} with title {issue.Title}.");
    }

    static Configuration LoadConfiguration(string path)
    {
        var input = File.ReadAllText(path);
        var deserializer = new YamlDotNet.Serialization.Deserializer();
        return deserializer.Deserialize<Configuration>(input);
    }

    static void ValidateOptions(Options opts)
    {
        if (!opts.TestMode && string.IsNullOrEmpty(opts.GithubToken))
        {
            ExitWithError(ExitCode.BadArguments, "Github token is required when not in Test mode.");
        }
        if (!File.Exists(opts.ConfigurationPath))
        {
            ExitWithError(ExitCode.FileNotFound, $"Configuration file did not exist at {opts.ConfigurationPath}.");
        }
    }

    static void HandleParseError(IEnumerable<Error> errs)
    {
        ExitWithError(ExitCode.BadArguments, "Error occurred while parsing command line arguments.");
    }

    static void ExitWithError(ExitCode exitCode, string message)
    {
        Console.WriteLine(message);
        Environment.Exit((int)exitCode);
    }
}
