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
    public class CustomAttributesWebApi : IClassFixture<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesWebApi(RemoteServiceFixtures.CustomAttributesWebApi fixture, ITestOutputHelper output)
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
                    _fixture.Get();

                    //This transaction trace will appear as error trace instead of transaction trace.
                    _fixture.Get404();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributes";
            var expectedTracedErrorPathAsync = @"WebTransaction/WebAPI/My/CustomErrorAttributes";

            var expectedTransactionTraceAttributes = new Dictionary<string, string>
            {
                { "key", "value" },
                { "foo", "bar" },
            };
            var expectedErrorTraceAttributes = new Dictionary<string, string>
            {
                { "hey", "dude" },
                { "faz", "baz" }
            };
            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "hey", "dude" },
                { "faz", "baz" }
            };
            var expectedTransactionEventAttributes = new Dictionary<string, string>
            {
                { "key", "value" },
                { "foo", "bar" }
            };

            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == expectedTransactionName)
                .FirstOrDefault();

            var errorTrace = _fixture.AgentLog.GetErrorTraces()
                .Where(trace => trace.Path == expectedTracedErrorPathAsync)
                .FirstOrDefault();


            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assertions.ErrorTraceHasAttributes(expectedErrorTraceAttributes, ErrorTraceAttributeType.User, errorTrace),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAttributes, TransactionEventAttributeType.User, transactionEvent),
                () => Assert.Single(errorEvents),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.User, errorEvents[0].Events[0])
            );
        }
    }
}
