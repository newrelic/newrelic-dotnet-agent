// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    [NetFrameworkTest]
    public class ChangeLogDirectoryTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        private const string CustomLogFileNameFromConfigBase = "customLogFileName";
        private const string CustomLogFileNameFromConfig = CustomLogFileNameFromConfigBase + ".log";
        private const string CustomAuditLogFileNameFromConfig = CustomLogFileNameFromConfigBase + "_audit.log";

        public ChangeLogDirectoryTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    var testLoggingDirectory = Path.Combine("C:\\", "IntegrationTestWorkingDirectory", _fixture.UniqueFolderName,
                        "TestLoggingDirectory");

                    Directory.CreateDirectory(testLoggingDirectory);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "info");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "directory", testLoggingDirectory);
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "fileName", CustomLogFileNameFromConfig);
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "auditLog", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.True(File.Exists(Path.Combine(_fixture.DestinationNewRelicLogFileDirectoryPath, CustomLogFileNameFromConfig)));
            Assert.True(File.Exists(Path.Combine(_fixture.DestinationNewRelicLogFileDirectoryPath, CustomAuditLogFileNameFromConfig)));
        }
    }
}
