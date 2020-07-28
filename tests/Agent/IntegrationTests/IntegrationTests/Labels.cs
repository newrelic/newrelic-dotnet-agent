﻿using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class Labels : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public Labels(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(fixture.DestinationNewRelicConfigFilePath, new[] { "configuration" }, "labels", "foo:bar;zip:zap");
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
            var connectMatch = _fixture.AgentLog.TryGetLogLine(@".*? NewRelic DEBUG: Invoking ""connect"" with : (?<payload>.*)");
            Assert.NotNull(connectMatch);
            var connectPayloadJson = connectMatch.Groups["payload"].Value;
            var connectPayload = JArray.Parse(connectPayloadJson);

            NrAssert.Multiple
            (
                () => Assert.Equal(2, connectPayload[0]["labels"].AsJEnumerable().Count()),
                () => Assert.Equal("foo", connectPayload[0]["labels"][0]["label_type"]),
                () => Assert.Equal("bar", connectPayload[0]["labels"][0]["label_value"]),
                () => Assert.Equal("zip", connectPayload[0]["labels"][1]["label_type"]),
                () => Assert.Equal("zap", connectPayload[0]["labels"][1]["label_value"])
            );
        }
    }
}
