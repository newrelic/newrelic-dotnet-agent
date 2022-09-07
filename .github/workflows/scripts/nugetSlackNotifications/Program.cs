using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8618

namespace nugetSlackNotifications
{
    public class Program
    {
        private static readonly HttpClient client = new();

        static async Task Main(string[] args)
        {
            List<Tuple<string, string, string>> newVersions = new();

            foreach (string package in args)
            {
                string response = await client.GetStringAsync($"https://api.nuget.org/v3/registration5-semver1/{package}/index.json");
                response = response.Replace("@", ""); // to be able to deserialize the necessary "@id" parameter successfully

                SearchResult? searchResult = JsonSerializer.Deserialize<SearchResult>(response);
                if (searchResult is null) continue;

                foreach (Item item in searchResult.items)
                {
                    // need to get the most recent and previous catalog entries
                    // to display previous and new version
                    Catalogentry latestCatalogEntry;
                    Catalogentry previousCatalogEntry;

                    if (item.items is not null) // standard json format from nuget.org
                    {
                        latestCatalogEntry = item.items[^1].catalogEntry; // latest release
                        previousCatalogEntry = item.items[^2].catalogEntry; // next-latest release
                    }
                    else // if item.items is null the json structure is weird and we have to use different properties
                    {
                        // need to make another request to get the page with individual release listings
                        Item? page = JsonSerializer.Deserialize<Item>(await client.GetStringAsync(item.id));
                        if (page is null) continue;

                        latestCatalogEntry = page.items[^1].catalogEntry; // latest release
                        previousCatalogEntry = page.items[^2].catalogEntry; // next-latest release
                    }
                    if (latestCatalogEntry.published > DateTime.Now.AddDays(-1))
                        newVersions.Add(new Tuple<string, string, string>(latestCatalogEntry.id, previousCatalogEntry.version, latestCatalogEntry.version));
                }
            }

            if (newVersions.Count > 0) // only message channel if there's package updates to report
            {
                string msg = "Hi team! Dotty here :technologist::pager:\nThere's some new NuGet releases you should know about :arrow_heading_down::sparkles:";
                foreach (var t in newVersions)
                    msg += $"\n\t:package: {char.ToUpper(t.Item1[0]) + t.Item1[1..]} {t.Item2} :point_right: {t.Item3}";
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
        public string id { get; set; }
        public DateTime commitTimeStamp { get; set; }
        public int count { get; set; }
        public Release[] items { get; set; }
        // some packages like MySqlConnector and Serilog return json that will have this and not items
        public string upper { get; set; }
    }

    public class Release
    {
        public string id { get; set; }
        public Catalogentry catalogEntry { get; set; }
    }

    public class Catalogentry
    {
        public string id { get; set; }
        public DateTime published { get; set; }
        public string version { get; set; }
    }
}

#pragma warning restore CS8618
