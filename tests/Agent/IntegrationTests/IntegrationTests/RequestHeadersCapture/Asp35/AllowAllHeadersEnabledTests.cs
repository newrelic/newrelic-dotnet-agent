// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.RequestHeadersCapture.Asp35
{
    public class Asp35AllowAllHeadersEnabledTestsBase : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public Asp35AllowAllHeadersEnabledTestsBase(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    var customRequestHeaders = new Dictionary<string, string>
                    {
                        { "FOO", "bar" },
                        { "Cookie", "itsasecret" },
                        { "dashes-are-valid", "true" },
                        { "dashesarevalid", "false" }
                    };

                    _fixture.PostWithTestHeaders(customRequestHeaders);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.HarvestFinishedLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "WebTransaction/MVC/DefaultController/Index";
            var expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.accept", "text/html" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" },
                { "request.headers.foo", "bar" },
                { "request.headers.dashes-are-valid", "true" },
                { "request.headers.dashesarevalid", "false" }
            };

            var unexpectedAttributes = new List<string>
            {
                "request.headers.cookie",
                "request.headers.authorization",
                "request.headers.proxy-authorization",
                "request.headers.x-forwarded-for"
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

    [NetFrameworkTest]
    public class Asp35AllowAllHeadersEnabledTests_ConfigFile : Asp35AllowAllHeadersEnabledTestsBase
    {
        public Asp35AllowAllHeadersEnabledTests_ConfigFile(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.SetAllowAllHeaders(true)
                        .ForceTransactionTraces()
                        .EnableSpanEvents(true);
                }
            );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class Asp35AllowAllHeadersEnabledTests_EnvVar : Asp35AllowAllHeadersEnabledTestsBase
    {
        public Asp35AllowAllHeadersEnabledTests_EnvVar(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.SetAllowAllHeaders(false)
                        .ForceTransactionTraces()
                        .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
                }
            );

            fixture.Initialize();
        }
    }
}
