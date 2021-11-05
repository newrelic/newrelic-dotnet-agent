// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public abstract class EnvironmentTests<T> : NewRelicIntegrationTest<T> where T : RemoteApplicationFixture
    {
        protected readonly T _fixture;
        protected ConnectData _connectData;

        public EnvironmentTests(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: ExerciseApplication
            );
            _fixture.Initialize();
        }

        protected abstract string ExpectedNrConfig { get; }
        protected abstract string ExpectedAppConfig { get; }

        protected abstract void ExerciseApplication();

        [Fact]
        public void TestPlugins()
        {
            _connectData = _connectData ?? _fixture.AgentLog.GetConnectData();

            var plugins = _connectData?.Environment?.GetPluginList();

            Assert.NotEmpty(plugins);

            var hasSystem = plugins.Any(plugin => plugin.Contains("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
            var hasNetstandard = plugins.Any(plugin => plugin.Contains("netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken="));
            var hasCore = plugins.Any(plugin => plugin.Contains("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
            var hasNrAgentCore = plugins.Any(plugin => plugin.Contains("NewRelic.Agent.Core, Version="));

            NrAssert.Multiple(
                () => Assert.True(hasSystem || hasNetstandard),
                () => Assert.True(hasCore),
                () => Assert.True(hasNrAgentCore)
            );
        }

        [Fact]
        public void TestConfigPaths()
        {
            _connectData = _connectData ?? _fixture.AgentLog.GetConnectData();

            var nrConfig = _connectData?.Environment?.GetPropertyString("Initial NewRelic Config");
            var appConfig = _connectData?.Environment?.GetPropertyString("Application Config");

            NrAssert.Multiple(
                () => Assert.NotNull(nrConfig),
                () => Assert.EndsWith(ExpectedNrConfig, nrConfig),

                () => Assert.NotNull(appConfig),
                () => Assert.EndsWith(ExpectedAppConfig, appConfig)
            );
        }
    }

    [NetFrameworkTest]
    public class EnvironmentFrameworkTests : EnvironmentTests<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        public EnvironmentFrameworkTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture, output) { }

        protected override string ExpectedNrConfig => @"\newrelichome\newrelic.config";
        protected override string ExpectedAppConfig => @"\BasicMvcApplication\web.config";

        protected override void ExerciseApplication() => _fixture.Get();
    }

    [NetCoreTest]
    public class EnvironmentCoreTests : EnvironmentTests<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        public EnvironmentCoreTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
            : base(fixture, output) { }

        protected override string ExpectedNrConfig => Utilities.IsLinux ? @"/newrelichome/newrelic.config" : @"\newrelichome\newrelic.config";
        protected override string ExpectedAppConfig => Utilities.IsLinux ? @"/AspNetCoreMvcBasicRequestsApplication/appsettings.json" : @"\AspNetCoreMvcBasicRequestsApplication\appsettings.json";

        protected override void ExerciseApplication() => _fixture.Get();
    }
}
