// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomAttributes
{
    [NetFrameworkTest]
    public class CustomAttributesIgnoredErrorAttributesNotInTransactionTrace : NewRelicIntegrationTest<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesIgnoredErrorAttributesNotInTransactionTrace(RemoteServiceFixtures.CustomAttributesWebApi fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(10);
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(configPath, new[] { "configuration", "errorCollector", "ignoreClasses" }, "errorClass", "System.ArithmeticException");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get404();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
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
                "hey",
                "faz",
            };
            var unexpectedTranscationEventAttributes = new List<string>
            {
                "key",
                "foo",
                "hey",
                "faz",
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var errorTrace = _fixture.AgentLog.GetErrorTraces().FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents().FirstOrDefault();

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assert.Null(errorTrace),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedTranscationEventAttributes, TransactionEventAttributeType.User, transactionEvent)
            );
        }
    }
}
