// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicWebService : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = "BasicWebService";

        public BasicWebService() : base(new RemoteWebApplication(ApplicationDirectoryName, ApplicationType.Bounded))
        {
        }

        public void InvokeServiceHttp()
        {
            var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/HelloWorld";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    request.Content = new FormUrlEncodedContent(Enumerable.Empty<KeyValuePair<string, string>>());

                    using (var response = client.SendAsync(request).Result)
                    {
                        var responseString = response.Content.ReadAsStringAsync().Result;
                        Assert.Contains("Hello World", responseString);
                    }
                }
            }
        }

        public void InvokeServiceSoap()
        {
            const string soapEnvelope =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			    xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			    xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
			    <soap12:Body>
			        <HelloWorld xmlns=""BasicWebService"" />
			    </soap12:Body>
			</soap12:Envelope>";

            var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/HelloWorld";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

                    using (var response = client.SendAsync(request).Result)
                    {
                        var responseData = response.Content.ReadAsStringAsync().Result;
                        Assert.Contains("Hello World", responseData);
                    }
                }
            }
        }

        public void ThrowExceptionHttp()
        {
            var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/ThrowException";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    request.Content = new FormUrlEncodedContent(Enumerable.Empty<KeyValuePair<string, string>>());

                    using (var response = client.SendAsync(request).Result)
                    {
                        Assert.False(response.IsSuccessStatusCode);
                    }
                }
            }
        }

        public void ThrowExceptionSoap()
        {
            const string soapEnvelope =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
			<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
			    xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
			    xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
			    <soap12:Body>
			        <ThrowException xmlns=""BasicWebService"" />
			    </soap12:Body>
			</soap12:Envelope>";

            var address = $"http://{DestinationServerName}:{Port}/BasicWebService.asmx/ThrowException";

            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, address))
                {
                    request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

                    using (var response = client.SendAsync(request).Result)
                    {
                        Assert.False(response.IsSuccessStatusCode);
                    }
                }
            }
        }
    }
}
