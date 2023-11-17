// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentLogs
{
    [NetFrameworkTest]
    public class LogFileFormatTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public LogFileFormatTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
            // get the first log line and validate it's in the expected format
            var firstLogLine = _fixture.AgentLog.GetFileLines().First();

            var match = Regex.Match(firstLogLine,
                @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3} NewRelic .{6}: \[pid: \d{1,}, tid: \d{1,}\] .*");

            Assert.True(match.Success);
            Assert.Single(match.Groups);
            Assert.Equal(firstLogLine, match.Groups[0].Value);
        }
    }
}
