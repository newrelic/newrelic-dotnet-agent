// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class CustomAttributesMvc : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public CustomAttributesMvc(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
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
                exerciseApplication: () => _fixture.GetCustomAttributes("foo", "bar", "zip", "zap")
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedAttributes = new Dictionary<string, string>
            {
                { "foo", "bar" },
                { "zip", "zap" },
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionSample);

            Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.User, transactionSample);
        }
    }
}
