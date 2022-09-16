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
        private static readonly HttpClient client = new(new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });

        static async Task Main(string[] args)
        {
            List<Tuple<string, string, string, string>> newVersions = new();
            foreach (string package in args)
            {
                string response = await client.GetStringAsync($"https://api.nuget.org/v3/registration5-gz-semver2/{package}/index.json");

                SearchResult? searchResult = JsonSerializer.Deserialize<SearchResult>(response);
                if (searchResult is null) continue;
                Item item = searchResult.items[^1]; // get the most recent group

                // need to make another request to get the page with individual release listings
                Page? page = JsonSerializer.Deserialize<Page>(await client.GetStringAsync(item.id));
                if (page is null) continue;

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

                if (latestCatalogEntry.published > DateTime.Now.AddDays(-10) && !(await latestCatalogEntry.isPrerelease()))
                    newVersions.Add(new Tuple<string, string, string, string>(package, previousCatalogEntry.version, latestCatalogEntry.version, $"https://www.nuget.org/packages/{package}/"));
            }

            if (newVersions.Count > 0) // only message channel if there's package updates to report
            {
                string msg = "Hi team! Dotty here :technologist::pager:\nThere's some new NuGet releases you should know about :arrow_heading_down::sparkles:";
                foreach (var t in newVersions)
                    msg += $"\n\t:package: {char.ToUpper(t.Item1[0]) + t.Item1[1..]} {t.Item2} :point_right: <{t.Item4}|{t.Item3}>";
                msg += $"\nThanks and have a wonderful {DateTime.Now.DayOfWeek}.";

                StringContent jsonContent = new(
                    JsonSerializer.Serialize(new
                    {
                        text = msg
                    }),
                    Encoding.UTF8,
                    "application/json");

                await client.PostAsync(Environment.GetEnvironmentVariable("webhook"), jsonContent);
            }
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
