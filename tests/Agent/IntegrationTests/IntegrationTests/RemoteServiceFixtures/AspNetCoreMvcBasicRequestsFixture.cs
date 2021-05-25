// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Net;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcBasicRequestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMvcBasicRequestsApplication";
        private const string ExecutableName = @"AspNetCoreMvcBasicRequestsApplication.exe";
        public AspNetCoreMvcBasicRequestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded, true, true, true))
        {
        }

        public void Get()
        {
            var address = $"http://localhost:{Port}/";
            DownloadStringAndAssertContains(address, "<html>");
        }
        public void GetCORSPreflight()
        {
            var address = $"http://localhost:{Port}/Home/About";
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Method = "OPTIONS";
            request.Headers.Add("Origin", "http://example.com");
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "X-Requested-With");

            var response = (HttpWebResponse)request.GetResponse();
            Assert.True(response.StatusCode == HttpStatusCode.NoContent);
        }

        public void MakePostRequestWithCustomRequestHeader(Dictionary<string, string> customHeadersToAdd)
        {
            var address = $"http://localhost:{Port}/";
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Method = "POST";
            request.Referer = "http://example.com";
            request.Host = "FakeHost";
            request.UserAgent = "FakeUserAgent";
            request.Accept = "text/html";

            //add custom header
            foreach (var pairs in customHeadersToAdd)
            {
                request.Headers.Add(pairs.Key, pairs.Value);
            }

            //send some data in the request body
            var bodyData = Encoding.Default.GetBytes("Hello");
            request.ContentLength = bodyData.Length;
            var newStream = request.GetRequestStream();
            newStream.Write(bodyData, 0, bodyData.Length);
            newStream.Close();



            var response = (HttpWebResponse)request.GetResponse();
            Assert.True(response.StatusCode == HttpStatusCode.OK);
        }

        public void ThrowException()
        {
            var address = $"http://localhost:{Port}/Home/ThrowException";
            var webClient = new WebClient();
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }

        public void ThrowExceptionWithMessage(string exceptionMessage)
        {
            var address = $"http://localhost:{Port}/ExpectedErrorTest/ThrowExceptionWithMessage?exceptionMessage={exceptionMessage}";
            var webClient = new WebClient();
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }

        public void ReturnADesiredStatusCode(int statusCode)
        {
            var address = $"http://localhost:{Port}/ExpectedErrorTest/ReturnADesiredStatusCode?statusCode={statusCode}";
            var webClient = new WebClient();
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }
        

        public void ThrowCustomException()
        {
            var address = $"http://localhost:{Port}/ExpectedErrorTest/ThrowCustomException";
            var webClient = new WebClient();
            Assert.Throws<WebException>(() => webClient.DownloadString(address));
        }

        public void GetWithData(string requestParameter)
        {
            var address = $"http://localhost:{Port}/Home/Query?data={requestParameter}";
            DownloadStringAndAssertContains(address, "<html>");
        }

        public void GetHttpClient()
        {
            var address = $"http://localhost:{Port}/Home/HttpClient";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetHttpClientTaskCancelled()
        {
            var address = $"http://localhost:{Port}/Home/HttpClientTaskCancelled";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetHttpClientFactory()
        {
            var address = $"http://localhost:{Port}/Home/HttpClientFactory";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetTypedHttpClient()
        {
            var address = $"http://localhost:{Port}/Home/TypedHttpClient";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetCallAsyncExternal()
        {
            var address = $"http://localhost:{Port}/DetachWrapper/CallAsyncExternal";
            DownloadStringAndAssertEqual(address, "Worked");
        }
    }

    public class HSMAspNetCoreMvcBasicRequestsFixture : AspNetCoreMvcBasicRequestsFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }
    }
}
