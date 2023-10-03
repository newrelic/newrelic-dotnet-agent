// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Configuration
{
    public abstract class GuidConfigurationTest<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        // Legacy configuration values
        const string CORE_GUID = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}";
        const string FRAMEWORK_GUID = "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}";

        private readonly TFixture _fixture;

        bool IsCore => _fixture.RemoteApplication.IsCoreApp;

        public GuidConfigurationTest(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"HttpClientDriver Get");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    // Intentionally use 'incorrect' GUID
                    _fixture.RemoteApplication.ProfilerGuidOverride = IsCore ? FRAMEWORK_GUID : CORE_GUID;
                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("trace");
                }
            );

            _fixture.Initialize();
        }

        const string CORE_LOG = "New Relic .NET CoreCLR Agent";
        const string FRAMEWORK_LOG = "New Relic .NET Agent";

        [Fact]
        public void Test()
        {
            var match = IsCore ? CORE_LOG : FRAMEWORK_LOG;

            // Profiler should flag correctly regardless
            Assert.True(_fixture.ProfilerLog.GetFileLines().Any(l => l.EndsWith(match)),
                $"Expected '{match}' in Profiler, but was not found.");
        }
    }

    [NetFrameworkTest]
    public class GuidConfigurationTest_FW : GuidConfigurationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        public GuidConfigurationTest_FW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output) { }
    }

    [NetCoreTest]
    public class GuidConfigurationTest_Core : GuidConfigurationTest<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public GuidConfigurationTest_Core(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output) { }
    }
}
