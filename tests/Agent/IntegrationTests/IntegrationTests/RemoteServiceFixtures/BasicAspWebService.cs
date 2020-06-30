/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using OpenQA.Selenium;
using OpenQA.Selenium.IE;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class BasicAspWebService : RemoteApplicationFixture
    {
        private IWebDriver _driver;
        public BasicAspWebService() : base(new RemoteWebApplication("BasicAspWebService", ApplicationType.Bounded))
        {
            var driverService = InternetExplorerDriverService.CreateDefaultService();
            driverService.HideCommandPromptWindow = true;
            driverService.SuppressInitialDiagnosticInformation = true;

            var options = new InternetExplorerOptions
            {
                IntroduceInstabilityByIgnoringProtectedModeSettings = true,
                IgnoreZoomLevel = true
            };

            _driver = new InternetExplorerDriver(driverService, options);
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
