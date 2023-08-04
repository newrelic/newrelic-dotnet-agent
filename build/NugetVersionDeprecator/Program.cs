using System.Net.Http.Headers;
using System.Net;
using System.Text;
using CommandLine;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Octokit;
using Repository = NuGet.Protocol.Core.Types.Repository;

namespace NugetVersionDeprecator;

internal class Program
{
    private const string RepoUrl = "https://api.nuget.org/v3/index.json";

    private const string NerdGraphQuery = @"
{
  docs {
    agentReleases(agentName: DOTNET) {
      version
      eolDate
    }
  }
}";

    private const string NerdGraphSample = @"
{
  ""data"": {
    ""docs"": {
      ""agentReleases"": [
        {
          ""eolDate"": ""2024-06-08"",
          ""version"": ""0.0.1""
        },
        {
          ""eolDate"": ""2024-06-08"",
          ""version"": ""9.9.0.0""
        },
        {
          ""eolDate"": ""2024-05-19"",
          ""version"": ""9.8.1.0""
        },
        {
          ""eolDate"": ""2024-05-05"",
          ""version"": ""9.8.0.0""
        },
        {
          ""eolDate"": ""2024-04-13"",
          ""version"": ""9.7.1.0""
        },
        {
          ""eolDate"": ""2024-04-04"",
          ""version"": ""9.7.0.0""
        },
        {
          ""eolDate"": ""2024-03-15"",
          ""version"": ""9.6.1.0""
        },
        {
          ""eolDate"": ""2024-02-24"",
          ""version"": ""9.6.0.0""
        },
        {
          ""eolDate"": ""2024-02-03"",
          ""version"": ""9.5.1.0""
        },
        {
          ""eolDate"": ""2024-02-01"",
          ""version"": ""9.5.0.0""
        },
        {
          ""eolDate"": ""2024-01-18"",
          ""version"": ""9.4.0.0""
        },
        {
          ""eolDate"": ""2024-01-04"",
          ""version"": ""9.3.0.0""
        },
        {
          ""eolDate"": ""2023-11-18"",
          ""version"": ""9.2.0.0""
        },
        {
          ""eolDate"": ""2023-11-02"",
          ""version"": ""9.1.1.0""
        },
        {
          ""eolDate"": ""2023-10-26"",
          ""version"": ""9.1.0.0""
        },
        {
          ""eolDate"": ""2023-09-16"",
          ""version"": ""9.0.0.0""
        },
        {
          ""eolDate"": ""2021-11-15"",
          ""version"": ""8.9.130.0""
        },
        {
          ""eolDate"": ""2021-10-18"",
          ""version"": ""8.8.83.0""
        },
        {
          ""eolDate"": ""2021-10-03"",
          ""version"": ""8.7.75.0""
        },
        {
          ""eolDate"": ""2021-08-28"",
          ""version"": ""8.6.45.0""
        },
        {
          ""eolDate"": ""2021-08-16"",
          ""version"": ""8.5.186.0""
        },
        {
          ""eolDate"": ""2021-07-30"",
          ""version"": ""8.4.880.0""
        },
        {
          ""eolDate"": ""2023-08-25"",
          ""version"": ""8.41.1.0""
        },
        {
          ""eolDate"": ""2023-07-21"",
          ""version"": ""8.41.0.0""
        },
        {
          ""eolDate"": ""2023-07-08"",
          ""version"": ""8.40.1.0""
        },
        {
          ""eolDate"": ""2023-06-08"",
          ""version"": ""8.40.0.0""
        },
        {
          ""eolDate"": ""2023-04-14"",
          ""version"": ""8.39.2.0""
        },
        {
          ""eolDate"": ""2023-03-17"",
          ""version"": ""8.39.1.0""
        },
        {
          ""eolDate"": ""2023-02-10"",
          ""version"": ""8.39.0.0""
        },
        {
          ""eolDate"": ""2023-01-26"",
          ""version"": ""8.38.0.0""
        },
        {
          ""eolDate"": ""2023-01-04"",
          ""version"": ""8.37.0.0""
        },
        {
          ""eolDate"": ""2022-12-08"",
          ""version"": ""8.36.0.0""
        },
        {
          ""eolDate"": ""2022-11-09"",
          ""version"": ""8.35.0.0""
        },
        {
          ""eolDate"": ""2022-10-26"",
          ""version"": ""8.34.0.0""
        },
        {
          ""eolDate"": ""2021-06-19"",
          ""version"": ""8.3.360.0""
        },
        {
          ""eolDate"": ""2022-10-12"",
          ""version"": ""8.33.0.0""
        },
        {
          ""eolDate"": ""2023-09-16"",
          ""version"": ""8.32.0.0""
        },
        {
          ""eolDate"": ""2023-08-17"",
          ""version"": ""8.31.0.0""
        },
        {
          ""eolDate"": ""2023-07-16"",
          ""version"": ""8.30.0.0""
        },
        {
          ""eolDate"": ""2023-06-25"",
          ""version"": ""8.29.0.0""
        },
        {
          ""eolDate"": ""2023-06-04"",
          ""version"": ""8.28.0.0""
        },
        {
          ""eolDate"": ""2023-04-30"",
          ""version"": ""8.27.139.0""
        },
        {
          ""eolDate"": ""2023-04-20"",
          ""version"": ""8.26.630.0""
        },
        {
          ""eolDate"": ""2023-03-11"",
          ""version"": ""8.25.214.0""
        },
        {
          ""eolDate"": ""2023-02-19"",
          ""version"": ""8.24.244.0""
        },
        {
          ""eolDate"": ""2023-01-15"",
          ""version"": ""8.23.107.0""
        },
        {
          ""eolDate"": ""2022-12-19"",
          ""version"": ""8.22.181.0""
        },
        {
          ""eolDate"": ""2021-05-16"",
          ""version"": ""8.2.216.0""
        },
        {
          ""eolDate"": ""2022-11-14"",
          ""version"": ""8.21.34.0""
        },
        {
          ""eolDate"": ""2022-10-01"",
          ""version"": ""8.19.353.0""
        },
        {
          ""eolDate"": ""2022-08-26"",
          ""version"": ""8.18.241.0""
        },
        {
          ""eolDate"": ""2022-07-22"",
          ""version"": ""8.17.438.0""
        },
        {
          ""eolDate"": ""2021-05-01"",
          ""version"": ""8.1.712.0""
        },
        {
          ""eolDate"": ""2021-04-19"",
          ""version"": ""8.1.709.0""
        },
        {
          ""eolDate"": ""2022-06-11"",
          ""version"": ""8.16.567.0""
        },
        {
          ""eolDate"": ""2022-04-22"",
          ""version"": ""8.15.455.0""
        },
        {
          ""eolDate"": ""2022-03-18"",
          ""version"": ""8.14.222.0""
        },
        {
          ""eolDate"": ""2022-02-12"",
          ""version"": ""8.13.798.0""
        },
        {
          ""eolDate"": ""2022-01-09"",
          ""version"": ""8.12.216.0""
        },
        {
          ""eolDate"": ""2021-12-17"",
          ""version"": ""8.11.157.0""
        },
        {
          ""eolDate"": ""2021-11-29"",
          ""version"": ""8.10.51.0""
        },
        {
          ""eolDate"": ""2021-03-07"",
          ""version"": ""8.0.0.0""
        },
        {
          ""eolDate"": ""2021-02-20"",
          ""version"": ""7.1.229.0""
        },
        {
          ""eolDate"": ""2021-01-22"",
          ""version"": ""7.0.2.0""
        },
        {
          ""eolDate"": ""2020-03-16"",
          ""version"": ""6.9.62.0""
        },
        {
          ""eolDate"": ""2020-03-02"",
          ""version"": ""6.8.172.0""
        },
        {
          ""eolDate"": ""2020-01-25"",
          ""version"": ""6.7.67.0""
        },
        {
          ""eolDate"": ""2020-01-12"",
          ""version"": ""6.6.5.0""
        },
        {
          ""eolDate"": ""2020-01-05"",
          ""version"": ""6.5.29.0""
        },
        {
          ""eolDate"": ""2019-12-19"",
          ""version"": ""6.4.21.0""
        },
        {
          ""eolDate"": ""2019-11-09"",
          ""version"": ""6.3.123.0""
        },
        {
          ""eolDate"": ""2023-01-28"",
          ""version"": ""6.27.0.0""
        },
        {
          ""eolDate"": ""2023-09-15"",
          ""version"": ""6.26.0.0""
        },
        {
          ""eolDate"": ""2023-01-15"",
          ""version"": ""6.25.0.0""
        },
        {
          ""eolDate"": ""2022-08-26"",
          ""version"": ""6.24.0.0""
        },
        {
          ""eolDate"": ""2022-04-22"",
          ""version"": ""6.22.0.0""
        },
        {
          ""eolDate"": ""2019-10-13"",
          ""version"": ""6.2.26.0""
        },
        {
          ""eolDate"": ""2021-03-07"",
          ""version"": ""6.22.0.0""
        },
        {
          ""eolDate"": ""2021-02-08"",
          ""version"": ""6.21.0.0""
        },
        {
          ""eolDate"": ""2020-12-18"",
          ""version"": ""6.20.166.0""
        },
        {
          ""eolDate"": ""2020-11-30"",
          ""version"": ""6.19.330.0""
        },
        {
          ""eolDate"": ""2020-11-02"",
          ""version"": ""6.18.139.0""
        },
        {
          ""eolDate"": ""2020-10-03"",
          ""version"": ""6.17.387.0""
        },
        {
          ""eolDate"": ""2020-08-28"",
          ""version"": ""6.16.178.0""
        },
        {
          ""eolDate"": ""2020-08-03"",
          ""version"": ""6.15.202.0""
        },
        {
          ""eolDate"": ""2019-09-29"",
          ""version"": ""6.1.48.0""
        },
        {
          ""eolDate"": ""2020-07-12"",
          ""version"": ""6.14.209.0""
        },
        {
          ""eolDate"": ""2020-06-15"",
          ""version"": ""6.13.366.0""
        },
        {
          ""eolDate"": ""2020-05-24"",
          ""version"": ""6.12.71.0""
        },
        {
          ""eolDate"": ""2020-05-17"",
          ""version"": ""6.12.64.0""
        },
        {
          ""eolDate"": ""2020-05-11"",
          ""version"": ""6.12.61.0""
        },
        {
          ""eolDate"": ""2020-05-04"",
          ""version"": ""6.11.613.0""
        },
        {
          ""eolDate"": ""2020-04-05"",
          ""version"": ""6.10.1.0""
        },
        {
          ""eolDate"": ""2019-09-14"",
          ""version"": ""6.0.0.0""
        },
        {
          ""eolDate"": ""2018-11-05"",
          ""version"": ""5.9.74.0""
        },
        {
          ""eolDate"": ""2018-10-14"",
          ""version"": ""5.8.28.0""
        },
        {
          ""eolDate"": ""2018-10-05"",
          ""version"": ""5.7.17.0""
        },
        {
          ""eolDate"": ""2018-09-24"",
          ""version"": ""5.6.53.0""
        },
        {
          ""eolDate"": ""2018-09-14"",
          ""version"": ""5.5.52.0""
        },
        {
          ""eolDate"": ""2018-08-27"",
          ""version"": ""5.4.16.0""
        },
        {
          ""eolDate"": ""2018-08-20"",
          ""version"": ""5.3.90.0""
        },
        {
          ""eolDate"": ""2018-07-22"",
          ""version"": ""5.2.87.0""
        },
        {
          ""eolDate"": ""2019-08-31"",
          ""version"": ""5.22.6.0""
        },
        {
          ""eolDate"": ""2019-08-23"",
          ""version"": ""5.21.74.0""
        },
        {
          ""eolDate"": ""2019-06-15"",
          ""version"": ""5.20.61.0""
        },
        {
          ""eolDate"": ""2019-04-26"",
          ""version"": ""5.19.47.0""
        },
        {
          ""eolDate"": ""2019-04-13"",
          ""version"": ""5.18.36.0""
        },
        {
          ""eolDate"": ""2019-04-04"",
          ""version"": ""5.17.59.0""
        },
        {
          ""eolDate"": ""2018-07-06"",
          ""version"": ""5.1.72.0""
        },
        {
          ""eolDate"": ""2019-03-11"",
          ""version"": ""5.16.71.0""
        },
        {
          ""eolDate"": ""2019-02-24"",
          ""version"": ""5.15.64.0""
        },
        {
          ""eolDate"": ""2019-02-09"",
          ""version"": ""5.14.43.0""
        },
        {
          ""eolDate"": ""2019-01-21"",
          ""version"": ""5.13.30.0""
        },
        {
          ""eolDate"": ""2019-01-07"",
          ""version"": ""5.12.13.0""
        },
        {
          ""eolDate"": ""2018-12-16"",
          ""version"": ""5.11.53.0""
        },
        {
          ""eolDate"": ""2018-12-01"",
          ""version"": ""5.10.59.0""
        },
        {
          ""eolDate"": ""2018-06-24"",
          ""version"": ""5.0.136.0""
        },
        {
          ""eolDate"": ""2018-05-20"",
          ""version"": ""4.6.29.0""
        },
        {
          ""eolDate"": ""2018-05-14"",
          ""version"": ""4.5.90.0""
        },
        {
          ""eolDate"": ""2018-04-29"",
          ""version"": ""4.4.60.0""
        },
        {
          ""eolDate"": ""2018-04-16"",
          ""version"": ""4.3.123.0""
        },
        {
          ""eolDate"": ""2018-03-31"",
          ""version"": ""4.2.185.0""
        },
        {
          ""eolDate"": ""2018-03-18"",
          ""version"": ""4.1.136.0""
        },
        {
          ""eolDate"": ""2018-03-17"",
          ""version"": ""4.1.134.0""
        },
        {
          ""eolDate"": ""2018-03-12"",
          ""version"": ""4.0.146.0""
        },
        {
          ""eolDate"": ""2017-10-29"",
          ""version"": ""3.9.146.0""
        },
        {
          ""eolDate"": ""2017-10-01"",
          ""version"": ""3.8.1.0""
        },
        {
          ""eolDate"": ""2017-09-25"",
          ""version"": ""3.7.135.0""
        },
        {
          ""eolDate"": ""2017-08-27"",
          ""version"": ""3.6.177.0""
        },
        {
          ""eolDate"": ""2017-08-19"",
          ""version"": ""3.5.107.0""
        },
        {
          ""eolDate"": ""2017-07-24"",
          ""version"": ""3.4.24.0""
        },
        {
          ""eolDate"": ""2017-07-11"",
          ""version"": ""3.3.38.0""
        },
        {
          ""eolDate"": ""2017-06-30"",
          ""version"": ""3.2.113.0""
        },
        {
          ""eolDate"": ""2017-06-20"",
          ""version"": ""3.1.65.0""
        },
        {
          ""eolDate"": ""2018-02-19"",
          ""version"": ""3.12.140.0""
        },
        {
          ""eolDate"": ""2018-01-26"",
          ""version"": ""3.11.296.0""
        },
        {
          ""eolDate"": ""2017-11-20"",
          ""version"": ""3.10.43.0""
        },
        {
          ""eolDate"": ""2017-05-29"",
          ""version"": ""3.0.79.0""
        },
        {
          ""eolDate"": ""2016-07-24"",
          ""version"": ""2.9.135.0""
        },
        {
          ""eolDate"": ""2016-06-21"",
          ""version"": ""2.8.134.0""
        },
        {
          ""eolDate"": ""2016-06-22"",
          ""version"": ""2.8.1.0""
        },
        {
          ""eolDate"": ""2016-05-16"",
          ""version"": ""2.7.60.0""
        },
        {
          ""eolDate"": ""2016-05-10"",
          ""version"": ""2.6.54.0""
        },
        {
          ""eolDate"": ""2016-03-15"",
          ""version"": ""2.5.112.0""
        },
        {
          ""eolDate"": ""2016-02-28"",
          ""version"": ""2.4.57.0""
        },
        {
          ""eolDate"": ""2016-02-08"",
          ""version"": ""2.3.126.0""
        },
        {
          ""eolDate"": ""2016-01-16"",
          ""version"": ""2.2.83.0""
        },
        {
          ""eolDate"": ""2017-04-23"",
          ""version"": ""2.25.208.0""
        },
        {
          ""eolDate"": ""2017-03-05"",
          ""version"": ""2.24.218.0""
        },
        {
          ""eolDate"": ""2017-02-05"",
          ""version"": ""2.23.2.0""
        },
        {
          ""eolDate"": ""2017-02-04"",
          ""version"": ""2.22.79.0""
        },
        {
          ""eolDate"": ""2017-01-23"",
          ""version"": ""2.21.84.0""
        },
        {
          ""eolDate"": ""2017-01-18"",
          ""version"": ""2.20.25.0""
        },
        {
          ""eolDate"": ""2017-01-09"",
          ""version"": ""2.20.24.0""
        },
        {
          ""eolDate"": ""2017-01-01"",
          ""version"": ""2.19.3.0""
        },
        {
          ""eolDate"": ""2016-12-27"",
          ""version"": ""2.18.35.0""
        },
        {
          ""eolDate"": ""2016-12-20"",
          ""version"": ""2.17.268.0""
        },
        {
          ""eolDate"": ""2016-12-19"",
          ""version"": ""2.17.266.0""
        },
        {
          ""eolDate"": ""2016-11-19"",
          ""version"": ""2.16.164.0""
        },
        {
          ""eolDate"": ""2016-10-22"",
          ""version"": ""2.15.186.1""
        },
        {
          ""eolDate"": ""2016-10-21"",
          ""version"": ""2.15.180.0""
        },
        {
          ""eolDate"": ""2016-10-11"",
          ""version"": ""2.14.53.0""
        },
        {
          ""eolDate"": ""2015-12-04"",
          ""version"": ""2.1.3.494""
        },
        {
          ""eolDate"": ""2016-10-02"",
          ""version"": ""2.13.38.0""
        },
        {
          ""eolDate"": ""2015-11-28"",
          ""version"": ""2.1.2.472""
        },
        {
          ""eolDate"": ""2016-09-13"",
          ""version"": ""2.12.146.0""
        },
        {
          ""eolDate"": ""2015-11-16"",
          ""version"": ""2.1.1.13""
        },
        {
          ""eolDate"": ""2015-10-11"",
          ""version"": ""2.1.0.5""
        },
        {
          ""eolDate"": ""2016-08-20"",
          ""version"": ""2.10.40.0""
        },
        {
          ""eolDate"": ""2015-06-07"",
          ""version"": ""2.0.9.15""
        },
        {
          ""eolDate"": ""2015-04-27"",
          ""version"": ""2.0.8.4""
        },
        {
          ""eolDate"": ""2015-02-20"",
          ""version"": ""2.0.7""
        },
        {
          ""eolDate"": ""2015-01-26"",
          ""version"": ""2.0.6""
        },
        {
          ""eolDate"": ""2014-12-07"",
          ""version"": ""2.0.5""
        },
        {
          ""eolDate"": ""2015-09-20"",
          ""version"": ""2.0.12.4""
        },
        {
          ""eolDate"": ""2015-08-22"",
          ""version"": ""2.0.11.1""
        },
        {
          ""eolDate"": ""2015-07-19"",
          ""version"": ""2.0.10.3""
        },
        {
          ""eolDate"": ""2024-04-11"",
          ""version"": ""10.9.1""
        },
        {
          ""eolDate"": ""2024-03-28"",
          ""version"": ""10.9.0""
        },
        {
          ""eolDate"": ""2024-03-14"",
          ""version"": ""10.8.0""
        },
        {
          ""eolDate"": ""2024-02-14"",
          ""version"": ""10.7.0""
        },
        {
          ""eolDate"": ""2024-01-24"",
          ""version"": ""10.6.0""
        },
        {
          ""eolDate"": ""2024-01-17"",
          ""version"": ""10.5.1""
        },
        {
          ""eolDate"": ""2024-01-12"",
          ""version"": ""10.5.0""
        },
        {
          ""eolDate"": ""2023-12-06"",
          ""version"": ""10.4.0""
        },
        {
          ""eolDate"": ""2023-10-26"",
          ""version"": ""10.3.0""
        },
        {
          ""eolDate"": ""2023-10-03"",
          ""version"": ""10.2.0""
        },
        {
          ""eolDate"": ""2024-07-18"",
          ""version"": ""10.13.0""
        },
        {
          ""eolDate"": ""2024-06-27"",
          ""version"": ""10.12.1""
        },
        {
          ""eolDate"": ""2024-06-26"",
          ""version"": ""10.12.0""
        },
        {
          ""eolDate"": ""2024-06-07"",
          ""version"": ""10.11.0""
        },
        {
          ""eolDate"": ""2024-04-26"",
          ""version"": ""10.10.0""
        },
        {
          ""eolDate"": ""2023-09-12"",
          ""version"": ""10.1.0""
        },
        {
          ""eolDate"": ""2024-07-19"",
          ""version"": ""10.0.0""
        },
        {
          ""eolDate"": ""2022-10-17"",
          ""version"": ""8.20.262.0""
        }
      ]
    }
  },
  ""extensions"": {
    ""nrOnly"": {
      ""_docs"": ""https://pages.datanerd.us/unified-api/nerdgraph-documentation/querying/debugging/"",
      ""allCacheHits"": [
        {
          ""count"": 1,
          ""name"": ""DocSite.API""
        }
      ],
      ""httpRequestLog"": [
        {
          ""body"": ""{\""query\"":\""query($capabilities: [String!]!, $scopeType: ScopeType!) {\\n  actor {\\n    capabilities(filter: {names: $capabilities}, scopeType: $scopeType) {\\n      name\\n    }\\n  }\\n}\\n\"",\""variables\"":{\""capabilities\"":[\""internal.read.nerd_graph_debug_data\""],\""scopeType\"":\""GLOBAL\""}}"",
          ""curl"": ""echo eyJxdWVyeSI6InF1ZXJ5KCRjYXBhYmlsaXRpZXM6IFtTdHJpbmchXSEsICRzY29wZVR5cGU6IFNjb3BlVHlwZSEpIHtcbiAgYWN0b3Ige1xuICAgIGNhcGFiaWxpdGllcyhmaWx0ZXI6IHtuYW1lczogJGNhcGFiaWxpdGllc30sIHNjb3BlVHlwZTogJHNjb3BlVHlwZSkge1xuICAgICAgbmFtZVxuICAgIH1cbiAgfVxufVxuIiwidmFyaWFibGVzIjp7ImNhcGFiaWxpdGllcyI6WyJpbnRlcm5hbC5yZWFkLm5lcmRfZ3JhcGhfZGVidWdfZGF0YSJdLCJzY29wZVR5cGUiOiJHTE9CQUwifX0= | base64 -D | curl -i -X POST 'https://ng-authorization-service.service.nr-ops.net/graphql' -H 'Content-Type: application/json' -H 'X-Login-Context: access-token:aXBrL0c2eVdSbzd1ZGlPbEZQK1pXcHdQTTNFTXhucXhEbTRrWitGSk4yZkVoZTNTL3cwWm4ycTlTQnprcW5TTStCbDJzVEUyMFV5ZUg3R1ZaS1RQaFlOa2l4NmJ3QmNXdkJrTkNqVU9YM0IyT2J4cXh3SUI0UmVLQWhNWkdPL04vVDNyRSsyRnM0c0hQV2hGVVdGdW1tbWsyR2g5M0NaQUNQakRpM1dRMWI4elpPdDFZOWhyT0lTbjBwTEhNRE5DTlBFQ2huUHhrUEZYTHg1dllTZVJId1ZPMXo1VzgreU9wc1hKZ3FqUDBvQzdoZUdkTXdCbkY0WTBDVnJ4bHlVVkRyd0lyRG1waERGcGx0OFBLZDY5bk9sQ3U1UThldHZVR0MvYW9OckZtcFlKak1jcTdWVUtBcDAwR1MyZVB0S0FTR1NFWkY3SVpzazVkRzd1QTJzSzh3PT0=' -d @-""
        }
      ]
    }
  }
}";

    static async Task Main(string[] args)

    {
        var options = Parser.Default.ParseArguments<Options>(args)
            .WithParsed(ValidateOptions)
            .WithNotParsed(HandleParseError)
            .Value;

        if (options.TestMode)
            Console.WriteLine("**** TEST MODE *** No Github Issues will be created.");

        var configuration = LoadConfiguration(options.ConfigurationPath);

        var deprecatedReleases = await QueryNerdGraphAsync(DateTime.UtcNow);

        if (deprecatedReleases.Any())
        {
            List<PackageDeprecationInfo> packagesToDeprecate = new();

            foreach (var package in configuration.Packages)
            {
                packagesToDeprecate.AddRange(await GetPackagesToDeprecateAsync(package, deprecatedReleases, DateTime.UtcNow.Date));
            }

            if (packagesToDeprecate.Any())
            {
                var message = ReportPackagesToDeprecate(packagesToDeprecate, deprecatedReleases);
                Console.WriteLine(message);

                if (!options.TestMode)
                    await CreateGhIssueAsync(message, options.GithubToken);
            }
        }
        else
        {
            Console.WriteLine("No eligible deprecated Agent released found.");
        }
    }

    private static async Task<List<AgentRelease>> QueryNerdGraphAsync(DateTime releaseDate)
    {
        // query the docs API to get a list of all .NET Agent versions

        // TODO: figure out how to query NerdGraph Api from code
        // for now, use a static sample response
        var nerdGraphResponse = NerdGraphSample;

        // parse the NerdGraph response -- we want to deserialize data.docs.agentReleases into an array of AgentRelease
        var parsedResponse = JObject.Parse(nerdGraphResponse);
        var allAgentReleases = parsedResponse.SelectToken("data.docs.agentReleases", false)?.ToObject<List<AgentRelease>>();
        if (allAgentReleases == null)
        {
            throw new Exception($"Unable to parse NerdGraph response: {Environment.NewLine}{nerdGraphResponse}");
        }

        var deprecatedReleases = allAgentReleases.Where(ar => ar.EolDate <= releaseDate).ToList();

        // TODO: refactor when doing real async, or make method non-async
        return await Task.FromResult(deprecatedReleases);
    }

    private static string ReportPackagesToDeprecate(List<PackageDeprecationInfo> packagesToDeprecate, List<AgentRelease> deprecatedReleases)
    {
        var sb = new StringBuilder();

        sb.AppendLine("The following NuGet packages should be deprecated:");
        foreach (var package in packagesToDeprecate)
        {
            var eolRelease = deprecatedReleases.Single(ar => ar.Version.StartsWith(package.PackageVersion));

            sb.AppendLine($"  * {package.PackageName} v{package.PackageVersion} (EOL as of {eolRelease.EolDate.ToShortDateString()})");
        }

        return sb.ToString();
    }

    static async Task<IEnumerable<PackageDeprecationInfo>> GetPackagesToDeprecateAsync(string packageName, List<AgentRelease> versionList, DateTime releaseDate)
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
            .Where(p =>
                p.DeprecationMetadata is null
                && string.Equals(p.Identity.Id, packageName, StringComparison.CurrentCultureIgnoreCase)).ToList();


        // intersect the two lists and build a list of NuGet versions that should be deprecated
        var deprecatedVersions = versionList.Select(ar => NuGetVersion.Parse(ar.Version)).ToList();
        var packagesToDeprecate = packages.Where(p => deprecatedVersions.Contains(p.Version)).ToList();

        return packagesToDeprecate.Select(p =>
            new PackageDeprecationInfo() { PackageName = p.PackageId, PackageVersion = p.Version.ToString() });
    }

    static async Task CreateGhIssueAsync(string message, string githubToken)
    {
        var ghClient = new GitHubClient(new Octokit.ProductHeaderValue("NugetVersionDeprecator"));
        var tokenAuth = new Credentials(githubToken);
        ghClient.Credentials = tokenAuth;


        var newIssue = new NewIssue($"chore(NugetDeprecator): NuGet packages need to be deprecated.")
        {
            Body = message
        };

        newIssue.Labels.Add("Deprecation");
        newIssue.Labels.Add("NuGet");

        await ghClient.Issue.Create("newrelic", "newrelic-dotnet-agent", newIssue);
    }

    static Configuration LoadConfiguration(string path)
    {
        var input = File.ReadAllText(path);
        var deserializer = new YamlDotNet.Serialization.Deserializer();
        return deserializer.Deserialize<Configuration>(input);
    }

    static void ValidateOptions(Options opts)
    {
        if (string.IsNullOrWhiteSpace(opts.ConfigurationPath) || string.IsNullOrWhiteSpace(opts.GithubToken))
        {
            ExitWithError(ExitCode.BadArguments, "One or more required arguments were not supplied.");
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
