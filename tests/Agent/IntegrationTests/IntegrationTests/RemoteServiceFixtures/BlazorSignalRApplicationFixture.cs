// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.AspNetCore.SignalR.Client;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public class BlazorSignalRApplicationFixture : RemoteApplicationFixture
{
    private const string ApplicationDirectoryName = "BlazorSignalRApplication";
    private const string ExecutableName = "BlazorSignalRApplication.exe";

    private IWebDriver _driver;

    public BlazorSignalRApplicationFixture()
        : base(new RemoteService(
            ApplicationDirectoryName,
            ExecutableName,
            targetFramework: "net10.0",
            ApplicationType.Bounded,
            true,
            true,
            true))
    {
        var chromeDriverService = ChromeDriverService.CreateDefaultService();
        chromeDriverService.HideCommandPromptWindow = true;
        chromeDriverService.SuppressInitialDiagnosticInformation = true;
        chromeDriverService.Port = RandomPortGenerator.NextPort();

        var chromeOptions = new ChromeOptions();
        chromeOptions.AddArgument("headless");

        _driver = new ChromeDriver(chromeDriverService, chromeOptions, TimeSpan.FromMinutes(2));
    }

    public void InvokeBlazorHub(string phrase)
    {
        var address = $"http://{DestinationServerName}:{Port}/";
        _driver.Navigate().GoToUrl(address);

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

        // Wait for the Blazor interactive circuit to be fully established.
        // The component sets window.__blazorCircuitReady in OnAfterRenderAsync,
        // which only fires after the SignalR circuit connects and the interactive
        // render completes (not during static prerendering).
        wait.Until(d =>
            (bool)((IJavaScriptExecutor)d).ExecuteScript("return window.__blazorCircuitReady === true"));

        var input = wait.Until(d =>
        {
            var el = d.FindElement(By.Id("phraseInput"));
            return el.Displayed && el.Enabled ? el : null;
        });

        input.Clear();
        input.SendKeys(phrase);

        var button = wait.Until(d =>
        {
            var el = d.FindElement(By.Id("sendButton"));
            return el.Displayed && el.Enabled ? el : null;
        });

        button.Click();

        wait.Until(d =>
        {
            var el = d.FindElement(By.Id("echoResult"));
            return el.Displayed;
        });
    }

    public void InvokeSignalRHub(string phrase)
    {
        var hubUrl = $"http://{DestinationServerName}:{Port}/echohub";

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .Build();

        try
        {
            hubConnection.StartAsync().GetAwaiter().GetResult();
            hubConnection.InvokeAsync("SendEcho", phrase).GetAwaiter().GetResult();
            hubConnection.StopAsync().GetAwaiter().GetResult();
        }
        finally
        {
            hubConnection.DisposeAsync().GetAwaiter().GetResult();
        }
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
