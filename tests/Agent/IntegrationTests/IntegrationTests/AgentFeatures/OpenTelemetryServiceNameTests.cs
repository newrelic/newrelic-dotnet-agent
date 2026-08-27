// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

public class OpenTelemetryServiceNameTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureCoreLatest>
{
    private readonly ConsoleDynamicMethodFixtureCoreLatest _fixture;

    private const string ExpectedAppName = "OtelBridgeServiceNameTestApp";

    public OpenTelemetryServiceNameTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                RemoveNewRelicAppNameFromAppSettings();

                _fixture.RemoteApplication.NewRelicConfig.EnableOpenTelemetry(true);
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("OTEL_SERVICE_NAME", ExpectedAppName);

                _fixture.AddCommand("HttpClientDriver Get");
            }
        );
        _fixture.Initialize();
    }

    private void RemoveNewRelicAppNameFromAppSettings()
    {
        // RemoteService.CopyToRemote() unconditionally writes NewRelic.AppName into appsettings.json,
        // which otherwise wins over OTEL_SERVICE_NAME in TryGetApplicationNames step 2; strip it here,
        // after CopyToRemote and before Start, so step 4b (OTEL_SERVICE_NAME) is reachable.
        var appSettingsFile = Path.Combine(_fixture.DestinationApplicationDirectoryPath, "appsettings.json");
        var jsonObj = JObject.Parse(File.ReadAllText(appSettingsFile));
        jsonObj.Remove("NewRelic.AppName");
        File.WriteAllText(appSettingsFile, jsonObj.ToString());
    }

    [Fact]
    public void AppNameComesFromOtelServiceName()
    {
        var match = _fixture.AgentLog.WaitForLogLine(@"Application name from OTEL_SERVICE_NAME Environment Variable\.", TimeSpan.FromSeconds(30));
        Assert.NotNull(match);
    }
}
