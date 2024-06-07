// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.AspNetCore
{
    [NetCoreTest]
    public class AllowAllHeadersEnabledTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public AllowAllHeadersEnabledTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    configModifier.SetAllowAllHeaders(true)
                    .EnableDistributedTrace().ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    var customRequestHeaders = new Dictionary<string, string> { { "foo", "bar" } };
                    _fixture.MakePostRequestWithCustomRequestHeader(customRequestHeaders);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "WebTransaction/MVC/Home/Index";
            var expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.accept", "text/html" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost:1234" },
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

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);
            var spanEvent = _fixture.AgentLog.TryGetSpanEvent(expectedTransactionName);

            Assert.NotNull(transactionSample);

            Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent, transactionSample);
            Assertions.SpanEventHasAttributes(expectedAttributes, SpanEventAttributeType.Agent, spanEvent);
            Assertions.TransactionEventHasAttributes(expectedAttributes, TransactionEventAttributeType.Agent, transactionEvent);

            Assertions.SpanEventDoesNotHaveAttributes(unexpectedAttributes, SpanEventAttributeType.Agent, spanEvent);
            Assertions.TransactionEventDoesNotHaveAttributes(unexpectedAttributes, TransactionEventAttributeType.Agent, transactionEvent);
            Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedAttributes, TransactionTraceAttributeType.Agent, transactionSample);
        }
    }
}
