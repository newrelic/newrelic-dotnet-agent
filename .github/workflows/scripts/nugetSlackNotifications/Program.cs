using NewRelic.Api.Agent;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

#pragma warning disable CS8618

namespace nugetSlackNotifications
{
    public class Program
    {
        // the semver2 registration endpoint returns gzip encoded json
        private static readonly HttpClient _client = new(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
        private static List<NugetVersionData> _newVersions = new();

        private static readonly int _daysToSearch = int.TryParse(Environment.GetEnvironmentVariable("DOTTY_DAYS_TO_SEARCH"), out var days) ? days : 1; // How many days of package release history to scan for changes
        private static readonly bool _testMode = bool.TryParse(Environment.GetEnvironmentVariable("DOTTY_TEST_MODE"), out var testMode) ? testMode : false;

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            foreach (string packageName in args)
            {
                await CheckPackage(packageName);
            }

            await AlertOnNewVersions();

        }

        [Transaction]
        static async Task CheckPackage(string packageName)
        {
            var response = await _client.GetStringAsync($"https://api.nuget.org/v3/registration5-gz-semver2/{packageName}/index.json");

            SearchResult? searchResult = JsonSerializer.Deserialize<SearchResult>(response);
            if (searchResult is null)
            {
                Log.Warning($"CheckPackage: null search result for package {packageName}");
                return;
            }

            Item item = searchResult.items[^1]; // get the most recent group

            // need to make another request to get the page with individual release listings
            Page? page = JsonSerializer.Deserialize<Page>(await _client.GetStringAsync(item.id));
            if (page is null)
            {
                Log.Warning($"CheckPackage: null page result for package {packageName}, item id {item.id}");
                return;
            }

            // need to get the most recent and previous catalog entries to display previous and new version
            Catalogentry latestCatalogEntry;
            Catalogentry previousCatalogEntry;

            // alternative json structure (see mysql.data)
            if (page.items[^1].catalogEntry is null)
            {
                latestCatalogEntry = page.items[^1].items[^1].catalogEntry; // latest release
                previousCatalogEntry = page.items[^1].items[^2].catalogEntry; // next-latest release
            }
            else // standard structure
            {
                latestCatalogEntry = page.items[^1].catalogEntry;
                previousCatalogEntry = page.items[^2].catalogEntry;
            }

            if (latestCatalogEntry.published > DateTime.Now.AddDays(-_daysToSearch) && !await latestCatalogEntry.isPrerelease())
            {
                Log.Information($"Package {packageName} has been updated in the past {_daysToSearch} days.");
                _newVersions.Add(new NugetVersionData(packageName, previousCatalogEntry.version, latestCatalogEntry.version, $"https://www.nuget.org/packages/{packageName}/"));
            }
            else
            {
                Log.Information($"Package {packageName} has not been updated in the past {_daysToSearch} days.");
            }
        }

        [Transaction]
        static async Task AlertOnNewVersions()
        {
            var webhook = Environment.GetEnvironmentVariable("DOTTY_WEBHOOK");

            if (_newVersions.Count > 0 && webhook != null && !_testMode) // only message channel if there's package updates to report AND we have a webhook from the environment AND we're not in test mode
            {
                string msg = "Hi team! Dotty here :technologist::pager:\nThere's some new NuGet releases you should know about :arrow_heading_down::sparkles:";
                foreach (var versionData in _newVersions)
                {
                    msg += $"\n\t:package: {char.ToUpper(versionData.PackageName[0]) + versionData.PackageName[1..]} {versionData.OldVersion} :point_right: <{versionData.Url}|{versionData.NewVersion}>";
                }
                msg += $"\nThanks and have a wonderful {DateTime.Now.DayOfWeek}.";

                Log.Information($"Alerting channel with message: {msg}");

                StringContent jsonContent = new(
                    JsonSerializer.Serialize(new
                    {
                        text = msg
                    }),
                    Encoding.UTF8,
                    "application/json");

                var webhookResult = await _client.PostAsync(Environment.GetEnvironmentVariable(webhook), jsonContent);
                if (webhookResult.StatusCode == HttpStatusCode.OK)
                {
                    Log.Information("Webhook invoke successfully");
                }
                else
                {
                    Log.Error($"Error invoking webhook: {webhookResult.StatusCode}");
                }
            }
            else
            {
                Log.Information($"Channel will not be alerted: # of new versions={_newVersions.Count}, webhook available={webhook != null}, test mode = {_testMode}");
            }
        }
    }

    public class NugetVersionData
    {
        public string PackageName { get; set; }
        public string OldVersion { get; set; }
        public string NewVersion { get; set; }
        public string Url { get; set; }

        public NugetVersionData(string packageName, string oldVersion, string newVersion, string url)
        {
            PackageName = packageName;
            OldVersion = oldVersion;
            NewVersion = newVersion;
            Url = url;
        }
    }

    public class SearchResult
    {
        public int count { get; set; }
        public Item[] items { get; set; }
    }

    public class Item
    {
        [JsonPropertyName("@id")]
        public string id { get; set; }
        public DateTime commitTimeStamp { get; set; }
    }

    public class Page
    {
        public Release[] items { get; set; }
    }

    public class Release
    {
        [JsonPropertyName("@id")]
        public string id { get; set; }
        public Catalogentry catalogEntry { get; set; }

        // only packages with alternative json structure (i.e. mysql.data) will have this
        public Release[] items { get; set; }
    }

    public class Version
    {
        public bool isPrerelease { get; set; }
    }

    public class Catalogentry
    {
        [JsonPropertyName("@id")]
        public string id { get; set; }
        public DateTime published { get; set; }
        public string version { get; set; }

        public async Task<bool> isPrerelease()
        {
            HttpClient client = new();
            string response = await client.GetStringAsync(id);
            Version? version = JsonSerializer.Deserialize<Version>(response);

            return version is null ? false : version.isPrerelease;
        }
    }
}

#pragma warning restore CS8618
