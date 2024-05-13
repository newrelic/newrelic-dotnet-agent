// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicMvcApplicationTestFixture : RemoteApplicationFixture
    {
        public const string ExpectedTransactionName = @"WebTransaction/MVC/DefaultController/Index";

        public string ResponseBody { get; private set; }

        public BasicMvcApplicationTestFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Bounded))
        {
        }

        public void GetHttpClient()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/HttpClient";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetHttpClientTaskCancelled()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/HttpClientTaskCancelled";
            GetStringAndAssertEqual(address, "Worked");
        }

        public HttpResponseHeaders GetRestSharpAsyncAwaitClientWithHeaders(string method, bool generic, bool cancelable)
        {
            var address = $"http://{DestinationServerName}:{Port}/RestSharp/AsyncAwaitClient?method={method}&generic={generic}&cancelable={cancelable}";

            var requestData = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId()),
                    new KeyValuePair<string, string>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
                };

            var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
            foreach (var header in headers)
                httpRequestMessage.Headers.Add(header.Key, header.Value);

            return Task.Run(() => _httpClient.SendAsync(httpRequestMessage)).Result.Headers;

        }

        public HttpResponseHeaders GetRestSharpTaskResultClientWithHeaders(string method, bool generic, bool cancelable)
        {
            var address = $"http://{DestinationServerName}:{Port}/RestSharp/TaskResultClient?method={method}&generic={generic}&cancelable={cancelable}";

            var requestData = new CrossApplicationRequestData("guid", false, "tripId", "pathHash");

            var headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId()),
                    new KeyValuePair<string, string>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
                };

            var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = HttpMethod.Get };
            foreach (var header in headers)
                httpRequestMessage.Headers.Add(header.Key, header.Value);

            return Task.Run(() => _httpClient.SendAsync(httpRequestMessage)).Result.Headers;
        }

        public void Get404(string Path = "DoesNotExist")
        {
            var address = $"http://{DestinationServerName}:{Port}/{Path}";

            GetAndAssertStatusCode(address, HttpStatusCode.NotFound);
        }

        public void GetIgnored()
        {
            var guid = Guid.NewGuid().ToString();
            var address = $"http://{DestinationServerName}:{Port}/Default/Ignored?data={guid}";
            GetStringAndAssertEqual(address, guid);
        }

        public void GetRouteWithAttribute()
        {
            var address = $"http://{DestinationServerName}:{Port}/foo/bar";
            GetStringAndIgnoreResult(address);
        }
        public void WaitForStartup()
        {
            AgentLog.WaitForConnect(Timing.TimeToConnect);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        var guid = Guid.NewGuid().ToString();
                        var address = $"http://{DestinationServerName}:{Port}/Default/Ignored?data={guid}";
                        httpClient.Timeout = TimeSpan.FromSeconds(1);
                        Task.Run(async () => Assert.Equal(guid, await httpClient.GetStringAsync(address))).Wait();
                        return;
                    }
                }
                catch (Exception)
                {
                    // try again in a bit, until we get a response in under 1 second (meaning server is up and stable) or 5 minutes passes
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }

            Assert.Fail(@"Did not receive a stable response (less than 1 second) after 5 minutes of attempts every 6 seconds.");
        }

        public void GetWithStatusCode(int statusCode)
        {
            GetWithHeaders(Enumerable.Empty<KeyValuePair<string, string>>(), "HandleThisRequestInGlobalAsax", $"?statusCode={statusCode}");
        }

        public void Request(HttpMethod method, string action = "Index")
        {
            RequestWithHeaders(method, null, Enumerable.Empty<KeyValuePair<string, string>>(), action);
        }

        public string Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default";
            ResponseBody = GetStringAndAssertContains(address, "<html>");

            return ResponseBody;
        }


        public string GetWithAsyncDisabled()
        {
            var address = $"http://{DestinationServerName}:{Port}/DisableAsyncSupport";
            ResponseBody = GetStringAndAssertContains(address, "<html>");

            return ResponseBody;
        }

        public string GetWithData(string requestParameter)
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/Query?data={requestParameter}";
            ResponseBody = GetStringAndAssertContains(address, "<html>");

            return ResponseBody;
        }


        public string GetNotHtmlContentType()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/NotHtmlContentType";
            // "ContentType means the HTTP Content-Type header...the body of the response still contains <html>
            // so this assertion is not as nonsensical as it might appear based on the name of the endpoint
            var result = GetStringAndAssertContains(address, "<html>");

            return result;
        }

        public HttpResponseHeaders RequestWithHeaders(HttpMethod method, string content, IEnumerable<KeyValuePair<string, string>> headers, string action = null, string queryString = null)
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/{action}{queryString}";

            using (var httpRequestMessage = new HttpRequestMessage { RequestUri = new Uri(address), Method = method })
            {
                foreach (var header in headers)
                    httpRequestMessage.Headers.Add(header.Key, header.Value);

                if (content != null)
                    httpRequestMessage.Content = new StringContent(content);

                return Task.Run(() => _httpClient.SendAsync(httpRequestMessage)).Result.Headers;
            }
        }

        public HttpResponseHeaders GetWithHeaders(IEnumerable<KeyValuePair<string, string>> headers, string action = null, string queryString = null)
        {
            return RequestWithHeaders(HttpMethod.Get, null, headers, action, queryString);
        }

        public HttpResponseHeaders PostWithTestHeaders(IEnumerable<KeyValuePair<string, string>> additionalHeaders = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "Referer", "http://example.com/" },
                { "Accept", "text/html" },
                { "Host", "fakehost" },
                { "User-Agent", "FakeUserAgent" }
            };

            return RequestWithHeaders(HttpMethod.Post, "Hello", headers.Concat(additionalHeaders ?? Enumerable.Empty<KeyValuePair<string, string>>()));
        }

        public HttpResponseHeaders GetWithCatHeader(bool includeCrossProcessIdHeader = true, CrossApplicationRequestData requestData = null)
        {
            var headers = new List<KeyValuePair<string, string>>();

            if (includeCrossProcessIdHeader)
                headers.Add(new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId()));
            if (requestData != null)
                headers.Add(new KeyValuePair<string, string>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData)));

            return GetWithHeaders(headers, "Index");
        }


        public HttpResponseHeaders GetWithCatHeaderWithRedirect()
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId())
            };

            return GetWithHeaders(headers, "DoRedirect");
        }

        public HttpResponseHeaders GetWithCatHeaderWithRedirectAndStatusCodeRollup()
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId())
            };

            return GetWithHeaders(headers, "HandleThisRequestInGlobalAsax", "?statusCode=301");
        }

        /// <summary>
        /// Makes a request, optionally including CAT headers, to the "ChainedWebRequest" endpoint (which will itself make a request).
        /// </summary>

        public HttpResponseHeaders GetWithCatHeaderChainedWebRequest(CrossApplicationRequestData requestData)
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId()),
                new KeyValuePair<string, string>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
            };

            const string action = "Index";
            var queryString = $"?chainedServerName={DestinationServerName}&chainedPortNumber={Port}&chainedAction={action}";
            return GetWithHeaders(headers, "ChainedWebRequest", queryString);
        }

        /// <summary>
        /// Makes a request, optionally including CAT headers, to the "ChainedHttpClient" endpoint (which will itself make a request).
        /// </summary>

        public HttpResponseHeaders GetWithCatHeaderChainedHttpClient(CrossApplicationRequestData requestData)
        {
            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("X-NewRelic-ID", GetXNewRelicId()),
                new KeyValuePair<string, string>("X-NewRelic-Transaction", GetXNewRelicRequestData(requestData))
            };

            const string action = "Index";
            var queryString = $"?chainedServerName={DestinationServerName}&chainedPortNumber={Port}&chainedAction={action}";
            return GetWithHeaders(headers, "ChainedHttpClient", queryString);
        }

        public HttpResponseHeaders GetWithUntrustedCatHeader()
        {
            var headers = new List<KeyValuePair<string, string>>();
            headers.Add(new KeyValuePair<string, string>("X-NewRelic-ID", GetUntrustedXNewRelicId()));
            return GetWithHeaders(headers, "Index");
        }

        public void GetCustomAttributes(string key1, string value1, string key2, string value2)
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/CustomParameters?key1={key1}&value1={value1}&key2={key2}&value2={value2}";
            ResponseBody = GetStringAndAssertContains(address, "Worked");

        }

        public void StartAgent()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/StartAgent";
            ResponseBody = GetStringAndAssertContains(address, "Worked");
        }

        private string GetXNewRelicId()
        {
            var accountId = AgentLog.GetAccountId();
            var applicationId = AgentLog.GetApplicationId();
            var accountAndApp = $@"{accountId}#{applicationId}";

            return HeaderEncoder.Base64Encode(accountAndApp, HeaderEncoder.IntegrationTestEncodingKey);
        }

        private string GetUntrustedXNewRelicId()
        {
            var invalidAccountId = "999999";
            var applicationId = AgentLog.GetApplicationId();
            var accountAndApp = $@"{invalidAccountId}#{applicationId}";

            return HeaderEncoder.Base64Encode(accountAndApp, HeaderEncoder.IntegrationTestEncodingKey);
        }

        private string GetXNewRelicRequestData(CrossApplicationRequestData requestData)
        {
            return HeaderEncoder.SerializeAndEncode(requestData, HeaderEncoder.IntegrationTestEncodingKey);
        }

        public void GetStaticResource()
        {
            var address = $"http://{DestinationServerName}:{Port}/bundles/modernizr?v=wBEWDufH_8Md-Pbioxomt90vm6tJN2Pyy9u9zHtWsPo1";
            GetStringAndIgnoreResult(address);
        }


        private static byte[] EncodeWithKey(string value, string key)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var keyBytes = Encoding.UTF8.GetBytes(key);

            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            return bytes;
        }

        public void ThrowException()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/ThrowException";
            GetAndAssertStatusCode(address, HttpStatusCode.InternalServerError);
        }

        public void SimulateLostTransaction()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/SimulateLostTransaction";
            GetStringAndAssertContains(address, "Worked");
        }

        public void GetRedis()
        {
            var address = $"http://{DestinationServerName}:{Port}/Redis/Get";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomInstrumentation()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/Get";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIgnoredByIgnoreTransactionWrapper()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionWrapper";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIgnoredByIgnoreTransactionTracerFactory()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionTracerFactory";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIgnoredByIgnoreTransactionWrapperAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentation/GetIgnoredByIgnoreTransactionWrapperAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomInstrumentationAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGet";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomInstrumentationAsyncCustomSegment()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGetCustomSegment";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomInstrumentationAsyncCustomSegmentAlternateParameterNaming()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/AsyncGetCustomSegmentAlternateParameterNaming";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetBackgroundThread()
        {
            var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/GetBackgroundThread";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetBackgroundThreadWithError()
        {
            try
            {
                var address = $"http://{DestinationServerName}:{Port}/CustomInstrumentationAsync/GetBackgroundThreadWithError";
                GetStringAndAssertEqual(address, "Worked");
            }
            catch (AggregateException)
            {
                // This is expected behavior.  We need to catch and swallow this exception here to
                // keep it from bubbling up to the test framework and failing the test.
            }
        }

        public string GetBrowserTimingHeader()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/GetBrowserTimingHeader";
            var result = GetStringAndAssertContains(address, "NREUM");

            return result;
        }


        public string GetHtmlWithCallToGetBrowserTimingHeader()
        {
            var address = $"http://{DestinationServerName}:{Port}/Default/GetHtmlWithCallToGetBrowserTimingHeader";
            ResponseBody = GetStringAndAssertContains(address, "Worked");

            return ResponseBody;
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Async/IoBoundConfigureAwaitFalseAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/Async/CpuBoundTasksAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

    }

    public class HSMBasicMvcApplicationTestFixture : BasicMvcApplicationTestFixture
    {
        public override string TestSettingCategory { get { return "HSM"; } }

    }

    public class SecurityPoliciesBasicMvcApplicationTestFixture : BasicMvcApplicationTestFixture
    {
        public override string TestSettingCategory { get { return "CSP"; } }

    }
}
