// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicWebFormsApplication : RemoteApplicationFixture
    {
        public BasicWebFormsApplication() : base(new RemoteWebApplication("BasicWebFormsApplication", ApplicationType.Bounded))
        {
            Actions
            (
                exerciseApplication: Get
            );
        }

        public void Get()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebForm1.aspx";
            GetStringAndAssertContains(address, "<html");
        }

        public void GetSlow()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebFormSlow.aspx";
            GetStringAndAssertContains(address, "<html");
        }

        public void Get404()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebFormThatDoesNotExist.aspx";

            GetAndAssertStatusCode(address, HttpStatusCode.NotFound);
        }

        public void GetWithQueryString(IEnumerable<KeyValuePair<string, string>> parameters, bool expectException)
        {
            var parametersAsStrings = parameters.Select(param => $"{param.Key}={param.Value}");
            var parametersAsString = string.Join("&", parametersAsStrings);
            var address = $"http://{DestinationServerName}:{Port}/WebForm1.aspx?{parametersAsString}";

            var exceptionOccurred = false;
            try
            {
                var result = _httpClient.GetStringAsync(address).Result;
                Assert.NotNull(result);
            }
            catch
            {
                exceptionOccurred = true;
            }

            Assert.Equal(expectException, exceptionOccurred);
        }

        public void GetWebFormWithTask()
        {
            var address = $"http://{DestinationServerName}:{Port}/WebFormWithTask.aspx";
            GetStringAndAssertContains(address, "<html");
        }
    }
}
