using NewRelic.Api.Agent;
using Octokit;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Repository = NuGet.Protocol.Core.Types.Repository;
using System.IO;

namespace nugetSlackNotifications
{
    public class Program
    {
        private static readonly HttpClient _client = new();
        private static List<NugetVersionData> _newVersions = new();

        private static readonly int _daysToSearch = int.TryParse(Environment.GetEnvironmentVariable("DOTTY_DAYS_TO_SEARCH"), out var days) ? int.Max(1, days) : 1; // How many days of package release history to scan for changes
        private static readonly bool _testMode = bool.TryParse(Environment.GetEnvironmentVariable("DOTTY_TEST_MODE"), out var testMode) ? testMode : false;
        private static readonly string _webhook = Environment.GetEnvironmentVariable("DOTTY_WEBHOOK");
        private static readonly string _githubToken = Environment.GetEnvironmentVariable("DOTTY_TOKEN");
        private static readonly DateTimeOffset _lastRunTimestamp = DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("DOTTY_LAST_RUN_TIMESTAMP"), out var timestamp) ? timestamp : DateTimeOffset.MinValue;
        private static readonly string _searchRootPath = Environment.GetEnvironmentVariable("DOTTY_SEARCH_ROOT_PATH") ?? ".";
        private const string PackageInfoFilename = "packageInfo.json";
        private const string ProjectsJsonFilename = "projectInfo.json";
        private const string Owner = "newrelic";
        private const string Repo = "newrelic-dotnet-agent";

        static async Task Main()
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            // searchTime is the date to search for package updates from.
            // If _lastRunTimestamp is not set, search from _daysToSearch days ago.
            // Otherwise, search from _lastRunTimestamp.
            var searchTime = _lastRunTimestamp == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow.Date.AddDays(-_daysToSearch) : _lastRunTimestamp;

            Log.Information($"Searching for package updates since {searchTime.ToUniversalTime():s}Z.");

            // initialize nuget repo
            var ps = new PackageSource("https://api.nuget.org/v3/index.json");
            var sourceRepository = Repository.Factory.GetCoreV3(ps);
            var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();
            var sourceCacheContext = new SourceCacheContext();

            if (!File.Exists(PackageInfoFilename))
            {
                Log.Error($"{PackageInfoFilename} not found in the current directory. Exiting.");
                return;
            }

            var packageInfoJson = await File.ReadAllTextAsync(PackageInfoFilename);
            var packageInfos = JsonSerializer.Deserialize<PackageInfo[]>(packageInfoJson);
            foreach (var package in packageInfos)
            {
                try
                {
                    await CheckPackage(package, metadataResource, sourceCacheContext, searchTime);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Caught exception while checking {package.PackageName} for updates.");
                    //await SendSlackNotification($"Dotty: caught exception while checking {package.PackageName} for updates: {ex}");
                }
            }

            if (!File.Exists(ProjectsJsonFilename))
            {
                Log.Error($"{ProjectsJsonFilename} not found in the current directory. Exiting.");
                return;
            }

            var updateLog = new List<string>();
            var projectInfoJson = await File.ReadAllTextAsync(ProjectsJsonFilename);
            var projectInfos = JsonSerializer.Deserialize<ProjectInfo[]>(projectInfoJson);
            foreach (var projectInfo in projectInfos)
            {
                var projectFile = Path.Combine(_searchRootPath, projectInfo.ProjectFile);
                if (!File.Exists(projectFile))
                {
                    Log.Warning($"Could not find {projectFile}, make sure projectFile path is relative.");
                    continue;
                }

                var projectLog = await CsprojHandler.UpdatePackageReferences(projectFile, _newVersions);
                if (projectLog.Count > 0)
                {
                    updateLog.Add($"**{projectInfo.ProjectFile}**");
                    updateLog.AddRange(projectLog);
                }
                
            }

            var prUrl = await CreateGithubPullRequestForNewVersions(projectInfos, string.Join('\n', updateLog));
            await AlertOnNewVersions(prUrl);

            // Currently don'y want to create issues, but may in the future
            // IF/When we do, this shuold be moved above the PR creation so we can link issues to the PR
            //await CreateGithubIssuesForNewVersions();
        }

        [Transaction]
        static async Task CheckPackage(PackageInfo package, PackageMetadataResource metadataResource,
            SourceCacheContext sourceCacheContext, DateTimeOffset searchTime)
        {
            var packageName = package.PackageName;

            var metaData = (await metadataResource.GetMetadataAsync(packageName, false, false, sourceCacheContext, NullLogger.Instance, System.Threading.CancellationToken.None)).OrderByDescending(p => p.Identity.Version).ToList();

            if (!metaData.Any())
            {
                Log.Warning($"CheckPackage: No metadata found for package {packageName}");
                return;
            }

            // get the most recent version of the package
            var latest = metaData.First();
            packageName = latest.Identity.Id;

            // get the second most recent version of the package (if there is one)
            var previous = metaData.Skip(1).FirstOrDefault();

            // check publish date
            if (latest.Published >= searchTime)
            {
                if (previous != null && (package.IgnorePatch || package.IgnoreMinor))
                {
                    var previousVersion = previous.Identity.Version;
                    var latestVersion = latest.Identity.Version;

                    if (package.IgnorePatch)
                    {
                        if (previousVersion.Major == latestVersion.Major && previousVersion.Minor == latestVersion.Minor)
                        {
                            Log.Information($"Package {packageName} ignores Patch version updates; the Minor version ({latestVersion.Major}.{latestVersion.Minor}) has not been updated.");
                            return;
                        }
                    }

                    if (package.IgnoreMinor)
                    {

                        if (previousVersion.Major == latestVersion.Major)
                        {
                            Log.Information($"Package {packageName} ignores Minor version updates; the Major version ({latestVersion.Major}) has not been updated.");
                            return;
                        }
                    }
                }

                var previousVersionDescription = previous?.Identity.Version.ToNormalizedString() ?? "Unknown";
                var latestVersionDescription = latest.Identity.Version.ToNormalizedString();
                Log.Information($"Package {packageName} was updated from {previousVersionDescription} to {latestVersionDescription}.");
                _newVersions.Add(new NugetVersionData(packageName, previousVersionDescription, latestVersionDescription, latest.PackageDetailsUrl.ToString(), latest.Published.Value.Date));
            }
            else
            {
                Log.Information($"Package {packageName} has NOT been updated.");
            }
        }

        [Transaction]
        static async Task AlertOnNewVersions(string prUrl)
        {

            if (_newVersions.Count > 0 && _webhook != null && !_testMode) // only message channel if there's package updates to report AND we have a webhook from the environment AND we're not in test mode
            {
                var msg = "Hi team! Dotty here :technologist::pager:\nThere's some new NuGet releases you should know about :arrow_heading_down::sparkles:";
                foreach (var versionData in _newVersions)
                {
                    msg += $"\n\t:package: {versionData.PackageName} {versionData.OldVersion} :point_right: <{versionData.Url}|{versionData.NewVersion}>";
                }

                msg += $"\n\nI did the work so you won't have too!";
                msg += $"\n" + prUrl + "\n";

                msg += $"\nThanks and have a wonderful {DateTime.Now.DayOfWeek}.";

                await SendSlackNotification(msg);
            }
            else
            {
                Log.Information($"Channel will not be alerted: # of new versions={_newVersions.Count}, webhook available={_webhook != null}, test mode={_testMode}");
            }
        }

        [Transaction]
        static async Task CreateGithubIssuesForNewVersions()
        {

            if (_newVersions.Count > 0 && _githubToken != null && !_testMode) // only message channel if there's package updates to report AND we have a GH token from the environment AND we're not in test mode
            {
                var ghClient = new GitHubClient(new ProductHeaderValue("Dotty-Robot"));
                var tokenAuth = new Credentials(_githubToken);
                ghClient.Credentials = tokenAuth;
                foreach (var versionData in _newVersions)
                {
                    var newIssue = new NewIssue($"Dotty: update tests for {versionData.PackageName} from {versionData.OldVersion} to {versionData.NewVersion}")
                    {
                        Body = $"Package [{versionData.PackageName}]({versionData.Url}) was updated from {versionData.OldVersion} to {versionData.NewVersion} on {versionData.PublishDate.ToShortDateString()}."
                    };
                    newIssue.Labels.Add("testing");
                    newIssue.Labels.Add("Core Technologies");
                    var issue = await ghClient.Issue.Create(Owner, Repo, newIssue);
                    Log.Information($"Created issue #{issue.Id} for {versionData.PackageName} update to {versionData.NewVersion} in newrelic/newrelic-dotnet-agent.");
                }
            }
            else
            {
                Log.Information($"Issues will not be created: # of new versions={_newVersions.Count}, token available={_webhook != null}, test mode={_testMode}");
            }
        }

        [Transaction]
        static async Task<string> CreateGithubPullRequestForNewVersions(IEnumerable<ProjectInfo> projectInfos, string updateLog)
        {

            if (_newVersions.Count > 0 && _githubToken != null && !_testMode) // only message channel if there's package updates to report AND we have a GH token from the environment AND we're not in test mode
            {
                var ghClient = new GitHubClient(new ProductHeaderValue("Dotty-Robot"));
                var tokenAuth = new Credentials(_githubToken);
                ghClient.Credentials = tokenAuth;

                var masterReference = await ghClient.Git.Reference.Get(Owner, Repo, "heads/main");
                var branchName = $"dotty/test-updates-{DateTime.Now.ToString("yyyy-MMM-dd")}";
                var newBranch = new NewReference($"refs/heads/{branchName}", masterReference.Object.Sha);
                await ghClient.Git.Reference.Create(Owner, Repo, newBranch);
                var latestCommit = await ghClient.Git.Commit.Get(Owner, Repo, masterReference.Object.Sha);
                var nt = new NewTree { BaseTree = latestCommit.Tree.Sha };
                foreach (var projectInfo in projectInfos)
                {
                    // string.Join with \n seems to allow github to see the changed lines and not the entire file as "changed"
                    nt.Tree.Add(new NewTreeItem
                    {
                        Path = projectInfo.ProjectFile,
                        Mode = "100644",
                        Type = TreeType.Blob,
                        Content = string.Join('\n', await File.ReadAllLinesAsync(Path.Combine(_searchRootPath, projectInfo.ProjectFile)))
                    });
                }

                var newTree = await ghClient.Git.Tree.Create(Owner, Repo, nt);
                var commitMessage = "test:Dotty instrumentation library updates for " + DateTime.Now.ToString("yyyy-MMM-dd");
                var newCommit = new NewCommit(commitMessage, newTree.Sha, masterReference.Object.Sha);
                var commit = await ghClient.Git.Commit.Create(Owner, Repo, newCommit);
                var branchref = await ghClient.Git.Reference.Update(Owner, Repo, $"heads/{branchName}", new ReferenceUpdate(commit.Sha));
                Log.Information($"Successfully created {branchName} branch.");

                var newPr = new NewPullRequest(commitMessage, branchName, "main");
                newPr.Body = "Dotty updated the following for your convenience.\n\n" + updateLog;
                var pullRequest = await ghClient.PullRequest.Create(Owner, Repo, newPr);
                Log.Information($"Successfully created PR for {branchName} at {pullRequest.HtmlUrl}");

                return pullRequest.HtmlUrl;
            }
            else
            {
                Log.Information($"Pull request will not be created: # of new versions={_newVersions.Count}, token available={_webhook != null}, test mode={_testMode}");
                return "";
            }
        }

        [Trace]
        static async Task SendSlackNotification(string msg)
        {
            if (_webhook != null && !_testMode)
            {
                Log.Information($"Alerting channel with message: {msg}");

                StringContent jsonContent = new(
                    JsonSerializer.Serialize(new
                    {
                        text = msg
                    }),
                    Encoding.UTF8,
                    "application/json");

                var webhookResult = await _client.PostAsync(_webhook, jsonContent);
                if (webhookResult.StatusCode == HttpStatusCode.OK)
                {
                    Log.Information("Webhook invoked successfully");
                }
                else
                {
                    Log.Error($"Error invoking webhook: {webhookResult.StatusCode}");
                }
            }
            else
            {
                Log.Error($"SendSlackNotification called but _webhook is null.  msg={msg}");
            }
        }
    }
}
