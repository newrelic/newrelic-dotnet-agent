// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    [NetFrameworkTest]
    public class LogLevelInfoAndAuditLogFalseTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public LogLevelInfoAndAuditLogFalseTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "info");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "auditLog", "false");
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
            Assert.NotNull(_fixture.AgentLog);
            var auditLogFile = Directory.EnumerateFiles(_fixture.DestinationNewRelicLogFileDirectoryPath, "newrelic_agent_*_audit.log")
                .FirstOrDefault();
            Assert.Null(auditLogFile);
        }
    }
}
