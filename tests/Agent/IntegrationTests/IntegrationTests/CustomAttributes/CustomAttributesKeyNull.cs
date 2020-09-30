// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomAttributes
{
    [NetFrameworkTest]
    public class CustomAttributesKeyNull : IClassFixture<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesKeyNull(RemoteServiceFixtures.CustomAttributesWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetKeyNull();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributesKeyNull";

            var unexpectedTransactionEventAttributes = new List<string>
            {
                "keywithnullvalue"
            };

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

            NrAssert.Multiple
            (
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedTransactionEventAttributes, TransactionEventAttributeType.User, transactionEvent)
            );
        }
    }
}
