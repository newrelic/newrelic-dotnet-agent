// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AspNetCoreMvcBasicRequestsApplication
{
    public class NewRelicDownloadSiteClient
    {
        private readonly HttpClient _httpClient;
        public NewRelicDownloadSiteClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.BaseAddress = new Uri("https://download.newrelic.com");
        }

        public async Task<string> GetLatestReleaseAsync()
        {
            var response = await _httpClient.GetAsync("");
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            return content;
        }
    }
}
