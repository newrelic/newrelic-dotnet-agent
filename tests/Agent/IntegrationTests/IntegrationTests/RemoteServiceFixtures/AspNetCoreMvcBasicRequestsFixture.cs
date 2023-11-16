// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcBasicRequestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMvcBasicRequestsApplication";
        private const string ExecutableName = @"AspNetCoreMvcBasicRequestsApplication.exe";
        public AspNetCoreMvcBasicRequestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/";
            GetStringAndAssertContains(address, "<html>");
        }
        public void GetCORSPreflight()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/About";

            using (var request = new HttpRequestMessage(HttpMethod.Options, address))
            {
                request.Headers.Add("Origin", "http://example.com");
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
                }
            }
        }

        public void MakePostRequestWithCustomRequestHeader(Dictionary<string, string> customHeadersToAdd)
        {
            var address = $"http://{DestinationServerName}:{Port}/";

            using (var request = new HttpRequestMessage(HttpMethod.Options, address))
            {
                request.Method = HttpMethod.Post;
                request.Headers.Referrer = new Uri("http://example.com/");
                request.Headers.Host = "fakehost:1234";
                request.Headers.Add("User-Agent", "FakeUserAgent");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

                request.Headers.Add("Proxy-Authorization", "Basic abc");
                request.Headers.Add("Authorization", "Basic xyz");
                request.Headers.Add("Cookie", "name1=value1; name2=value2; name3=value3");
                request.Headers.Add("X-Forwarded-For", "xyz");

                //add custom header
                foreach (var pairs in customHeadersToAdd)
                {
                    request.Headers.Add(pairs.Key, pairs.Value);
                }

                var bodyData = Encoding.Default.GetBytes("Hello");
                var byteContent = new ByteArrayContent(bodyData);
                request.Content = byteContent;

                using (var response = _httpClient.SendAsync(request).Result)
                {
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
            }
        }

        public void ThrowException()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/ThrowException";
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
        }

        public void ThrowExceptionWithMessage(string exceptionMessage)
        {
            var address = $"http://{DestinationServerName}:{Port}/ExpectedErrorTest/ThrowExceptionWithMessage?exceptionMessage={exceptionMessage}";
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
        }

        public void ReturnADesiredStatusCode(int statusCode)
        {
            var address = $"http://{DestinationServerName}:{Port}/ExpectedErrorTest/ReturnADesiredStatusCode?statusCode={statusCode}";
            GetAndAssertStatusCode(address, (HttpStatusCode)statusCode);
        }

        public void ThrowCustomException()
        {
            var address = $"http://{DestinationServerName}:{Port}/ExpectedErrorTest/ThrowCustomException";
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
        }

        public void GetWithData(string requestParameter)
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/Query?data={requestParameter}";
            GetStringAndAssertContains(address, "<html>");
        }

        public void GetHttpClient()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/HttpClient";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetHttpClientTaskCancelled()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/HttpClientTaskCancelled";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetHttpClientFactory()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/HttpClientFactory";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetTypedHttpClient()
        {
            var address = $"http://{DestinationServerName}:{Port}/Home/TypedHttpClient";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCallAsyncExternal()
        {
            var address = $"http://{DestinationServerName}:{Port}/DetachWrapper/CallAsyncExternal";
            GetStringAndAssertEqual(address, "Worked");
        }
    }

    public class HSMAspNetCoreMvcBasicRequestsFixture : AspNetCoreMvcBasicRequestsFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}
