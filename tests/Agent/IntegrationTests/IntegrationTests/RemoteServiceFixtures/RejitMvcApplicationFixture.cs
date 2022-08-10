// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Net;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreReJitMvcApplicationFixture : RejitMvcApplicationFixture
    {
        public AspNetCoreReJitMvcApplicationFixture() :
            this(useTieredCompilation: false)
        {
        }

        protected AspNetCoreReJitMvcApplicationFixture(bool useTieredCompilation)
            : base(new RemoteService("AspNetCoreMvcRejitApplication", "AspNetCoreMvcRejitApplication.exe", "netcoreapp3.1", ApplicationType.Bounded, true, true, true))
        {
            RemoteApplication.UseTieredCompilation = useTieredCompilation;
        }

        public override string DestinationServerName => "127.0.0.1";
    }

    public class AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation : AspNetCoreReJitMvcApplicationFixture
    {
        public AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation()
            : base(useTieredCompilation: true)
        {
        }
    }

    public class AspNetFrameworkReJitMvcApplicationFixture : RejitMvcApplicationFixture
    {
        public AspNetFrameworkReJitMvcApplicationFixture()
            : base(new RemoteWebApplication("RejitMvcApplication", ApplicationType.Bounded))
        {
        }
    }

    /// <summary>
    /// MVC web application behind the Rejit Integration tests. Based on a stripped down and altered version of the BasicMvcApplication.
    /// </summary>
    public class RejitMvcApplicationFixture : RemoteApplicationFixture
    {
        public RejitMvcApplicationFixture(RemoteApplication remoteApplication) : base(remoteApplication)
        {
        }

        /// <summary>
        /// Calls a simple endpoint outside of the CustomInstrumentationController to get the agent started up.
        /// </summary>
        public void InitializeApp()
        {
            var address = $"http://{DestinationServerName}:{Port}/";
            var result = new ExtendedTimeoutWebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Contains("It am working", result);
        }

        /// <summary>
        /// Calls an endpoint meant to test adding a node (tracerFactory, exactMethodMatcher) to an existing file.
        /// </summary>
        /// <param name="option">Selects which instrumented method to call within the action. Options: 0, 1</param>
        public void TestAddNode(int option)
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetAddNode/{option}";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestDeleteNode(int option)
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetDeleteNode/{option}";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestAddAttribute()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetAddAttribute";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestChangeAttributeValue()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetChangeAttributeValue";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestDeleteAttribute()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetDeleteAttribute";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestAddFile()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetAddFile";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestRenameFile()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetRenameFile";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        public void TestDeleteFile()
        {
            var address = $"http://{DestinationServerName}:{Port}/Rejit/GetDeleteFile";
            ExtendedTimeoutDownloadStringAndAssertEqual(address, "It am working");
        }

        /// <summary>
        /// When run together, the rejit tests sometimes timeout waiting for the site to spin up initially.
        /// This class extends the timeout significantly to allow things to spin up in their own time.
        /// </summary>
        private class ExtendedTimeoutWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                var webRequest = base.GetWebRequest(uri);
                webRequest.Timeout = 300 * 1000; // 5 minutes
                return webRequest;
            }
        }

        private string ExtendedTimeoutDownloadStringAndAssertEqual(string address, string expectedContent)
        {
            var result = new ExtendedTimeoutWebClient().DownloadString(address);

            Assert.NotNull(result);
            Assert.Equal(expectedContent, result);

            return result;

        }
    }
}
