// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicAspWebService : RemoteApplicationFixture
    {
        private IWebDriver _driver;
        public BasicAspWebService() : base(new RemoteWebApplication("BasicAspWebService", ApplicationType.Bounded))
        {
            var driverService = ChromeDriverService.CreateDefaultService();
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;
            _driver = new ChromeDriver(driverService);
        }

        public void InvokeAsyncCall()
        {
            var address = $"http://{DestinationServerName}:{Port}/TestClient.aspx";
            _driver.Navigate().GoToUrl(address);
        }

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
