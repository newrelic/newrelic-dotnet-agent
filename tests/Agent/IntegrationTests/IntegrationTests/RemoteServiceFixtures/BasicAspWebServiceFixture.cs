// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using OpenQA.Selenium.Edge;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicAspWebServiceFixture : RemoteApplicationFixture
    {
        private EdgeDriver _driver;
        public BasicAspWebServiceFixture() : base(new RemoteWebApplication("BasicAspWebService", ApplicationType.Bounded))
        {
            var driverService = EdgeDriverService.CreateDefaultService();
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;
            driverService.InitializationTimeout = TimeSpan.FromMinutes(2);
            driverService.Port = RandomPortGenerator.NextPort();

            var options = new EdgeOptions();

            _driver = new EdgeDriver(driverService, options, TimeSpan.FromMinutes(2));
        }

        public void InvokeAsyncCall()
        {
            var address = $"http://{DestinationServerName}:{Port}/TestClient.aspx";
            _driver.Navigate().GoToUrl(address);
        }

        public override string DestinationServerName { get; } = "localhost";

        public override void Dispose()
        {
            if (_driver != null)
            {
                _driver.Quit();
                _driver.Dispose();
                _driver = null;
            }

            base.Dispose();
        }
    }
}

#endif
