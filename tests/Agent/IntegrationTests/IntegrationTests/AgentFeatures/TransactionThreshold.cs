// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class TransactionThreshold : NewRelicIntegrationTest<RemoteServiceFixtures.OwinWebApiFixture>
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        public TransactionThreshold(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "transactionThreshold", "100000");
                },
                exerciseApplication: () => _fixture.Get()
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.False(_fixture.AgentLog.GetTransactionSamples().Any(), "Transaction trace found when none were expected.");
        }
    }
}
