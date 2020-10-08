// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetFrameworkTest]
    public class HighSecurityAndCustomAttributes : IClassFixture<HSMCustomAttributesWebApi>
    {
        private readonly HSMCustomAttributesWebApi _fixture;

        public HighSecurityAndCustomAttributes(HSMCustomAttributesWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetHighSecurityMode(true);
                    configModifier.SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.GetCustomErrorAttributes();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.ErrorEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                }

                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedTransactionTraceAttributes = new List<string>
            {
                "key",
                "foo",
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionSample);

            Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample);

            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var errorEventPayload = _fixture.AgentLog.GetErrorEvents().FirstOrDefault();

            var unexpectedErrorCustomAttributes = new List<string>
            {
                "hey",
                "faz"
            };

            NrAssert.Multiple(
                () => Assertions.ErrorTraceDoesNotHaveAttributes(unexpectedErrorCustomAttributes, ErrorTraceAttributeType.Intrinsic, errorTrace),
                () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorCustomAttributes, EventAttributeType.User, errorEventPayload.Events[0])
            );
        }
    }
}
