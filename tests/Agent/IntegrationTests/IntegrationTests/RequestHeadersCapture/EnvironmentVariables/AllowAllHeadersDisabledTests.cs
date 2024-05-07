// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.EnvironmentVariables
{
    
    public abstract class EnvironmentVariableAllowAllHeadersDisabledTestsBase : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public Dictionary<string, object> expectedAttributes;
        public List<string> unexpectedAttributes;

        public EnvironmentVariableAllowAllHeadersDisabledTestsBase(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    var customRequestHeaders = new Dictionary<string, string> { { "foo", "bar" } };

                    _fixture.PostWithTestHeaders(customRequestHeaders);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "WebTransaction/MVC/DefaultController/Index";

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

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersDisabledTests_Defaults : EnvironmentVariableAllowAllHeadersDisabledTestsBase
    {
        public EnvironmentVariableAllowAllHeadersDisabledTests_Defaults(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.accept", "text/html" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" }
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.foo",
                "request.headers.cookie",
                "request.headers.authorization",
                "request.headers.proxy-authorization",
                "request.headers.x-forwarded-for"
            };

            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    configModifier.SetAllowAllHeaders(true)
                        .ForceTransactionTraces()
                        .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "false");
                }
            );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersDisabledTests_Excludes_CommaDelimited : EnvironmentVariableAllowAllHeadersDisabledTestsBase
    {
        public EnvironmentVariableAllowAllHeadersDisabledTests_Excludes_CommaDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" }
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.referer",
                "request.headers.accept",
                "request.headers.foo",
                "request.headers.cookie",
                "request.headers.authorization",
                "request.headers.proxy-authorization",
                "request.headers.x-forwarded-for"
            };

            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    configModifier.SetAllowAllHeaders(true)
                        .ForceTransactionTraces()
                        .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "false");
                    fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_EXCLUDE", "request.headers.referer,request.headers.accept");
                }
            );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersDisabledTests_Excludes_CommaSpaceDelimited : EnvironmentVariableAllowAllHeadersDisabledTestsBase
    {
        public EnvironmentVariableAllowAllHeadersDisabledTests_Excludes_CommaSpaceDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" }
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.referer",
                "request.headers.accept",
                "request.headers.foo",
                "request.headers.cookie",
                "request.headers.authorization",
                "request.headers.proxy-authorization",
                "request.headers.x-forwarded-for"
            };

            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    configModifier.SetAllowAllHeaders(true)
                        .ForceTransactionTraces()
                        .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "false");
                    fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_EXCLUDE", "request.headers.referer, request.headers.accept");
                }
            );

            fixture.Initialize();
        }
    }
}
