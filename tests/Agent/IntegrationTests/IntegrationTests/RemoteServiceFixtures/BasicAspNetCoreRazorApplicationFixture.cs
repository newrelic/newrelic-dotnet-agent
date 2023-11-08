// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicAspNetCoreRazorApplicationFixture : RemoteApplicationFixture
    {
        private bool _responseCompressionEnabled;
        private const string ApplicationDirectoryName = @"BasicAspNetCoreRazorApplication";
        private const string ExecutableName = @"BasicAspNetCoreRazorApplication.exe";
        public BasicAspNetCoreRazorApplicationFixture()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, targetFramework: "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void SetResponseCompression(bool enabled)
        {
            _responseCompressionEnabled = enabled;
            SetAdditionalEnvironmentVariable("ENABLE_RESPONSE_COMPRESSION", enabled ? "1" : "0");
        }

        public string Get(string endpoint)
        {
            var address = $"http://{DestinationServerName}:{Port}/{endpoint}";

            if (_responseCompressionEnabled)
            {
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, address))
                {
                    requestMessage.Headers.Add("Accept-Encoding", "gzip");

                    using (var response = _httpClient.SendAsync(requestMessage).Result)
                    {
                        var resultBytes = response.Content.ReadAsByteArrayAsync().Result;
                        Assert.NotNull(resultBytes);

                        if (response.Content.Headers.ContentEncoding != null &&
                            response.Content.Headers.ContentEncoding.ToString() == "gzip")
                        {
                            using (var ms = new MemoryStream(resultBytes))
                            using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                            {
                                var reader = new StreamReader(gzip, Encoding.UTF8);

                                var result = reader.ReadToEndAsync().Result;
                                Assert.NotNull(result);
                                return result;
                            }
                        }
                        else // not compressed
                            return response.Content.ReadAsStringAsync().Result;
                    }
                }
            }

            return GetString(address);
        }
    }
}
