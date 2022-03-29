// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    [NetFrameworkTest]
    public class ApiAppNameChangeTests : NewRelicIntegrationTest<RemoteServiceFixtures.ApiAppNameChangeFixture>
    {

        private readonly RemoteServiceFixtures.ApiAppNameChangeFixture _fixture;

        public ApiAppNameChangeTests(RemoteServiceFixtures.ApiAppNameChangeFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, new[] { new KeyValuePair<string, string>("autoStart", "false") });
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedLogLineRegexes = new[]
            {
                @".+ Your New Relic Application Name\(s\): AgentApi",
                @".+ Your New Relic Application Name\(s\): AgentApi2"
            };
            var unexpectedLogLineRegexes = new[]
            {
                @".+ Your New Relic Application Name\(s\): " + _fixture.RemoteApplication.AppName
            };

            var actualLogLines = _fixture.AgentLog.GetFileLines();

            NrAssert.Multiple
            (
                () => Assertions.LogLinesExist(expectedLogLineRegexes, actualLogLines),
                () => Assertions.LogLinesNotExist(unexpectedLogLineRegexes, actualLogLines)
            );
        }
    }
}
