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
    public class AspNetCoreMinimalApiTestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMinimalApiApplication";
        private const string ExecutableName = @"AspNetCoreMinimalApiApplication.exe";
        public AspNetCoreMinimalApiTestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "net7.0", ApplicationType.Bounded, true, true, true))
        {
        }

        public void MinimalApiGet()
        {
            var address = $"http://localhost:{Port}/minimalapi";
            GetAndAssertSuccessStatus(address, true);
        }

        public void MinimalApiPost()
        {
            var address = $"http://localhost:{Port}/minimalapi";
            PostAndAssertSuccessStatus(address, true);
        }
    }
}
