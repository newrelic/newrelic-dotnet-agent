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

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.Owin
{
    [NetFrameworkTest]
    public abstract class AsyncAllowAllHeadersEnabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : RemoteServiceFixtures.OwinWebApiFixture
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        protected AsyncAllowAllHeadersEnabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.SetAllowAllHeaders(true);
                    configModifier.EnableDistributedTrace();
                    configModifier.ForceTransactionTraces();
                    configModifier.AddAttributesInclude("request.parameters.*");
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.PostAsync();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "WebTransaction/WebAPI/AsyncAwait/SimplePostAsync";
            var expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.uri", "/AsyncAwait/SimplePostAsync" },

                // Captured headers
                { "request.headers.accept", "application/json" },
                { "request.headers.host", "fakehost:1234" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.content-length", "7" },
                { "request.headers.user-agent", "FakeUserAgent" },
                { "request.headers.foo", "bar" }
            };

            var unexpectedAttributes = new List<string>
            {
                 "request.headers.cookie",
                 "request.headers.authorization",
                 "request.headers.proxy-authorization",
                 "request.headers.x-forwarded-For"
            };

            var transactionSamples = _fixture.AgentLog.GetTransactionSamples();
            //this is the transaction trace that is generally returned, but this 
            //is not necessarily always the case
            var traceToCheck = transactionSamples
                .Where(sample => sample.Path == expectedTransactionName)
                .FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);
            var spanEvent = _fixture.AgentLog.TryGetSpanEvent(expectedTransactionName);

            NrAssert.Multiple(
                () => Assert.NotNull(traceToCheck),
                () => Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent, traceToCheck),
                () => Assertions.SpanEventHasAttributes(expectedAttributes, SpanEventAttributeType.Agent, spanEvent),
                () => Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Agent, transactionEvent),
                () => Assertions.SpanEventDoesNotHaveAttributes(unexpectedAttributes, SpanEventAttributeType.Agent, spanEvent),
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedAttributes, TransactionEventAttributeType.Agent, transactionEvent),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAttributes, TransactionTraceAttributeType.Agent, traceToCheck)
             );
        }
    }

    public class OwinWebApiAsyncAllowAllHeadersEnabledTest : AsyncAllowAllHeadersEnabledTestsBase<RemoteServiceFixtures.OwinWebApiFixture>
    {
        public OwinWebApiAsyncAllowAllHeadersEnabledTest(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin3WebApiAsyncAllowAllHeadersEnabledTest : AsyncAllowAllHeadersEnabledTestsBase<RemoteServiceFixtures.Owin3WebApiFixture>
    {
        public Owin3WebApiAsyncAllowAllHeadersEnabledTest(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin4WebApiAsyncAllowAllHeadersEnabledTest : AsyncAllowAllHeadersEnabledTestsBase<RemoteServiceFixtures.Owin4WebApiFixture>
    {
        public Owin4WebApiAsyncAllowAllHeadersEnabledTest(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
