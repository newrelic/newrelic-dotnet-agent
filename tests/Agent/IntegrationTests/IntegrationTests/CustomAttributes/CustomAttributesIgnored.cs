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
    public class CustomAttributesIgnored : NewRelicIntegrationTest<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesIgnored(RemoteServiceFixtures.CustomAttributesWebApi fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = _fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("debug");
                    configModifier.AddAttributesExclude("*");
                    configModifier.AddAttributesInclude("name");
                    configModifier.AddAttributesInclude("foo");
                    configModifier.AddAttributesInclude("hey");
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    // Generates a transaction trace.
                    _fixture.Get();

                    // Generates an error trace.
                    _fixture.Get404();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                }

            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributes";
            var expectedTracedErrorPathAsync = @"WebTransaction/WebAPI/My/CustomErrorAttributes";

            var expectedTransactionTraceAttributes = new Dictionary<string, string>
            {
                { "foo", "bar" },
            };
            var unexpectedTransactionTraceAttributes = new List<string>
            {
                "key",
                "hey",
                "faz",
            };
            var expectedErrorTraceAttributes = new Dictionary<string, string>
            {
                { "hey", "dude" },
            };
            var unexpectedErrorTraceAttributes = new List<string>
            {
                "faz",
                "foo",
                "key",
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "hey", "dude" },
            };
            var unexpectedErrorEventAttributes = new List<string>
            {
                "faz",
                "foo",
                "key",
            };

            var expectedTransactionEventAttributes = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };
            var unexpectedTranscationEventAttributes = new List<string>
            {
                "key",
                "faz",
                "hey"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == expectedTransactionName)
                .FirstOrDefault();

            Assert.NotNull(transactionSample);


            var errorTrace = _fixture.AgentLog.GetErrorTraces()
                .Where(trace => trace.Path == expectedTracedErrorPathAsync)
                .FirstOrDefault();

            Assert.NotNull(errorTrace);

            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

            Assert.NotNull(transactionEvent);

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assertions.ErrorTraceHasAttributes(expectedErrorTraceAttributes, ErrorTraceAttributeType.User, errorTrace),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAttributes, TransactionEventAttributeType.User, transactionEvent),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assertions.ErrorTraceDoesNotHaveAttributes(unexpectedErrorTraceAttributes, ErrorTraceAttributeType.User, errorTrace),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedTranscationEventAttributes, TransactionEventAttributeType.User, transactionEvent),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.User, errorEvents[0]),
                () => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedErrorEventAttributes, EventAttributeType.User, errorEvents[0])
            );

        }
    }
}
