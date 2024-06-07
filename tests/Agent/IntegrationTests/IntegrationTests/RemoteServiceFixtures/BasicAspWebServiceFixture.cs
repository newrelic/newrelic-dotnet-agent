// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicAspWebServiceFixture : RemoteApplicationFixture
    {
        private IWebDriver _driver;
        public BasicAspWebServiceFixture() : base(new RemoteWebApplication("BasicAspWebService", ApplicationType.Bounded))
        {
            var chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.HideCommandPromptWindow = true;
            chromeDriverService.SuppressInitialDiagnosticInformation = true;
            chromeDriverService.Port = RandomPortGenerator.NextPort();

            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("headless");

            _driver = new ChromeDriver(chromeDriverService, chromeOptions, TimeSpan.FromMinutes(2));
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
