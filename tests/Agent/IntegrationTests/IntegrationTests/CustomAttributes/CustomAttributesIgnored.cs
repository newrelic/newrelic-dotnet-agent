// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
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
            _fixture.Actions(setupConfiguration: () =>
            {
                var configPath = _fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ForceTransactionTraces();

                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                CommonUtils.AddXmlNodeInNewRelicConfig(configPath, new[] { "configuration", "attributes" }, "exclude", "*");
                CommonUtils.AddXmlNodeInNewRelicConfig(configPath, new[] { "configuration", "attributes" }, "include", "name");
                CommonUtils.AddXmlNodeInNewRelicConfig(configPath, new[] { "configuration", "attributes" }, "include", "foo");
                CommonUtils.AddXmlNodeInNewRelicConfig(configPath, new[] { "configuration", "attributes" }, "include", "hey");
            },

            exerciseApplication: () =>
                {
                    _fixture.Get();

                    //This transaction trace will appear as error trace instead of transaction trace.
                    _fixture.Get404();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
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
            var errorTrace = _fixture.AgentLog.GetErrorTraces()
                .Where(trace => trace.Path == expectedTracedErrorPathAsync)
                .FirstOrDefault();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

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
