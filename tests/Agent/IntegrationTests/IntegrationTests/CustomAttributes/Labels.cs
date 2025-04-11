// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json.Linq;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.CustomAttributes
{
    public class Labels : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public Labels(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var connectPayload = _fixture.AgentLog.GetConnectData();
            Assert.NotNull(connectPayload);


            NrAssert.Multiple
            (
                () => Assert.Equal(2, connectPayload.Labels.Count()),
                () => Assert.Equal("foo", (connectPayload.Labels.ElementAt(0) as JObject)["label_type"]),
                () => Assert.Equal("bar", (connectPayload.Labels.ElementAt(0) as JObject)["label_value"]),
                () => Assert.Equal("zip", (connectPayload.Labels.ElementAt(1) as JObject)["label_type"]),
                () => Assert.Equal("zap", (connectPayload.Labels.ElementAt(1) as JObject)["label_value"])
            );
        }
    }
}
