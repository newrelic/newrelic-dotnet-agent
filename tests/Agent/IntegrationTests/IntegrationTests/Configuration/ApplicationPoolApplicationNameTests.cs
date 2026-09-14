// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Configuration;

public abstract class ApplicationPoolApplicationNameTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private const string PoolName = "IntegrationTestAppPool";
    private const string MappedAppName = "IntegrationTestPoolMappedAppName";

    private readonly TFixture _fixture;

    protected ApplicationPoolApplicationNameTestsBase(TFixture fixture, string appPoolEnvironmentVariableName, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.RemoteApplication.SetAdditionalEnvironmentVariable(appPoolEnvironmentVariableName, PoolName);

        _fixture.AddCommand("HttpClientDriver Get");

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                _fixture.RemoteApplication.NewRelicConfig
                    .DeleteApplicationName()
                    .AddApplicationPoolMapping(PoolName, MappedAppName);

                RemoveAppNameFromExecutableConfig();
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedLogLineRegexes = new[]
        {
            @"Application name from application pool mapping in newrelic\.config\.",
            @".+ Your New Relic Application Name\(s\): " + MappedAppName
        };

        var actualLogLines = _fixture.AgentLog.GetFileLines();

        NrAssert.Multiple
        (
            () => Assertions.LogLinesExist(expectedLogLineRegexes, actualLogLines)
        );
    }

    // the harness stamps a NewRelic.AppName setting that outranks the application pool mapping
    private void RemoveAppNameFromExecutableConfig()
    {
        var applicationDirectoryPath = _fixture.RemoteApplication.DestinationApplicationDirectoryPath;

        if (_fixture.RemoteApplication.IsCoreApp)
        {
            var appSettingsFilePath = Path.Combine(applicationDirectoryPath, "appsettings.json");
            if (!File.Exists(appSettingsFilePath))
            {
                return;
            }

            var jsonObj = JObject.Parse(File.ReadAllText(appSettingsFilePath));
            jsonObj.Remove("NewRelic.AppName");

            using (var file = File.CreateText(appSettingsFilePath))
            using (var writer = new JsonTextWriter(file))
            {
                jsonObj.WriteTo(writer);
            }

            return;
        }

        var appConfigFilePath = Directory.GetFiles(applicationDirectoryPath, "*.exe.config").FirstOrDefault();
        if (appConfigFilePath == null)
        {
            return;
        }

        var document = new XmlDocument();
        document.Load(appConfigFilePath);

        var settingNode = document.SelectSingleNode("/configuration/appSettings/add[@key='NewRelic.AppName']");
        settingNode?.ParentNode?.RemoveChild(settingNode);

        document.Save(appConfigFilePath);
    }
}

[Trait("Runtime", "Framework")]
public class ApplicationPoolApplicationNameFWLatestTests : ApplicationPoolApplicationNameTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public ApplicationPoolApplicationNameFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, "APP_POOL_ID", output)
    {
    }
}

[Trait("Runtime", "Core")]
public class ApplicationPoolApplicationNameCoreLatestTests : ApplicationPoolApplicationNameTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public ApplicationPoolApplicationNameCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, "ASPNETCORE_IIS_APP_POOL_ID", output)
    {
    }
}
