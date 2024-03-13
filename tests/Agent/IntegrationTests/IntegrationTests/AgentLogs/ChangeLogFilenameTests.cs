// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    [NetFrameworkTest]
    public abstract class ChangeLogFilenameTestsBase : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public enum ConfigSettingTestCase
        {
            ConfigFile,
            EnvVar,
            Both
        }

        protected ConfigSettingTestCase _testCase;

        private const string CustomLogFileNameFromConfigBase = "customLogFileName";
        private const string CustomLogFileNameFromConfig = CustomLogFileNameFromConfigBase + ".log";
        private const string CustomAuditLogFileNameFromConfig = CustomLogFileNameFromConfigBase + "_audit.log";

        private const string CustomLogFileNameFromEnvVarBase = "customLogFileNameFromEnvVar";
        private const string CustomLogFileNameFromEnvVar = CustomLogFileNameFromEnvVarBase + ".log";
        private const string CustomAuditLogFileNameFromEnvVar = CustomLogFileNameFromEnvVarBase + "_audit.log";

        public ChangeLogFilenameTestsBase(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output, ConfigSettingTestCase testCase) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AgentLogExpected = false; // So the test doesn't spend three minutes waiting for an agent log to appear in the expected location
            _testCase = testCase;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "info");
                    if (_testCase == ConfigSettingTestCase.ConfigFile || _testCase == ConfigSettingTestCase.Both)
                    {
                        CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "fileName", CustomLogFileNameFromConfig);
                    }
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "auditLog", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                }
            );
            if (_testCase == ConfigSettingTestCase.EnvVar || _testCase == ConfigSettingTestCase.Both)
            {
                _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG", CustomLogFileNameFromEnvVar);
            }
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedLogFileName = (_testCase == ConfigSettingTestCase.EnvVar || _testCase == ConfigSettingTestCase.Both) ? CustomLogFileNameFromEnvVar : CustomLogFileNameFromConfig;
            var expectedAuditLogFileName = (_testCase == ConfigSettingTestCase.EnvVar || _testCase == ConfigSettingTestCase.Both) ? CustomAuditLogFileNameFromEnvVar : CustomAuditLogFileNameFromConfig;

            Assert.True(File.Exists(Path.Combine(_fixture.DestinationNewRelicLogFileDirectoryPath, expectedLogFileName)));
            Assert.True(File.Exists(Path.Combine(_fixture.DestinationNewRelicLogFileDirectoryPath, expectedAuditLogFileName)));
        }
    }

    public class ChangeLogFilenameInConfigTests : ChangeLogFilenameTestsBase
    {
        public ChangeLogFilenameInConfigTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output, ConfigSettingTestCase.ConfigFile)
        {
        }
    }

    public class ChangeLogFilenameWithEnvVarTests : ChangeLogFilenameTestsBase
    {
        public ChangeLogFilenameWithEnvVarTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output, ConfigSettingTestCase.EnvVar)
        {
        }
    }

    public class ChangeLogFilenameInBothTests : ChangeLogFilenameTestsBase
    {
        public ChangeLogFilenameInBothTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output, ConfigSettingTestCase.Both)
        {
        }
    }

}
