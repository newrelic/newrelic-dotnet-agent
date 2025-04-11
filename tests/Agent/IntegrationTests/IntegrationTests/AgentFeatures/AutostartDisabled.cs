// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class AutoStartDisabled : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public AutoStartDisabled(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.StartAgent();
                    _fixture.Get();
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var events = _fixture.AgentLog.GetTransactionEvents();

            // Calls to "Get" endpoint will have the transaction name WebTransaction/MVC/DefaultController/Index
            var getEvents = events.Where(e => e?.IntrinsicAttributes?["name"]?.ToString() == "WebTransaction/MVC/DefaultController/Index");

            // The first call to the "Get" endpoint should be ignored because the agent wasn't enabled.
            Assert.Single(getEvents);

            // There may or may not be a transaction event generated for the "StartAgent" endpoint, but it doesn't really matter. Either behavior is acceptable.
        }
    }
}
