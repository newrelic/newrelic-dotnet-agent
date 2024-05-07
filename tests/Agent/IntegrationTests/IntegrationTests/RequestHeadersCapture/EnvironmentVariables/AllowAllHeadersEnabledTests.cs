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
    public abstract  class EnvironmentVariableAllowAllHeadersEnabledTests_Base : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public Dictionary<string, object> expectedAttributes;
        public List<string> unexpectedAttributes;

        public EnvironmentVariableAllowAllHeadersEnabledTests_Base(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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
    public class EnvironmentVariableAllowAllHeadersEnabledTests_Defaults : EnvironmentVariableAllowAllHeadersEnabledTests_Base
    {
        public EnvironmentVariableAllowAllHeadersEnabledTests_Defaults(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
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

            unexpectedAttributes = new List<string>
            {
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
                   configModifier.SetAllowAllHeaders(false)
                       .ForceTransactionTraces()
                       .EnableSpanEvents(true);

                   fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
               }
           );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersEnabledTests_Includes_CommaDelimited : EnvironmentVariableAllowAllHeadersEnabledTests_Base
    {
        public EnvironmentVariableAllowAllHeadersEnabledTests_Includes_CommaDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.accept", "text/html" },
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.content-length",
                "request.headers.host",
                "request.headers.user-agent",
                "request.headers.foo",
                "request.headers.dashes-are-valid",
                "request.headers.dashesarevalid",
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
                   configModifier.SetAllowAllHeaders(false)
                       .ForceTransactionTraces()
                       .EnableSpanEvents(true);

                   fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
                   fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.referer,request.headers.accept");
               }
           );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersEnabledTests_Includes_CommaSpaceDelimited : EnvironmentVariableAllowAllHeadersEnabledTests_Base
    {
        public EnvironmentVariableAllowAllHeadersEnabledTests_Includes_CommaSpaceDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.referer", "http://example.com/" },
                { "request.headers.accept", "text/html" },
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.content-length",
                "request.headers.host",
                "request.headers.user-agent",
                "request.headers.foo",
                "request.headers.dashes-are-valid",
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
                   configModifier.SetAllowAllHeaders(false)
                       .ForceTransactionTraces()
                       .EnableSpanEvents(true);

                   fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
                   fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_INCLUDE", "request.headers.referer, request.headers.accept");
               }
           );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersEnabledTests_Excludes_CommaDelimited : EnvironmentVariableAllowAllHeadersEnabledTests_Base
    {
        public EnvironmentVariableAllowAllHeadersEnabledTests_Excludes_CommaDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" },
                { "request.headers.foo", "bar" },
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.referer",
                "request.headers.accept",
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
                    configModifier.SetAllowAllHeaders(false)
                       .ForceTransactionTraces()
                       .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
                    fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_EXCLUDE", "request.headers.referer,request.headers.accept,request.headers.cookie");
                }
            );

            fixture.Initialize();
        }
    }

    [NetFrameworkTest]
    public class EnvironmentVariableAllowAllHeadersEnabledTests_Excludes_CommaSpaceDelimited : EnvironmentVariableAllowAllHeadersEnabledTests_Base
    {
        public EnvironmentVariableAllowAllHeadersEnabledTests_Excludes_CommaSpaceDelimited(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
            expectedAttributes = new Dictionary<string, object>
            {
                { "request.method", "POST" },
                { "request.headers.content-length", "5" },
                { "request.headers.host", "fakehost" },
                { "request.headers.user-agent", "FakeUserAgent" },
                { "request.headers.foo", "bar" },
            };

            unexpectedAttributes = new List<string>
            {
                "request.headers.referer",
                "request.headers.accept",
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
                    configModifier.SetAllowAllHeaders(false)
                       .ForceTransactionTraces()
                       .EnableSpanEvents(true);

                    fixture.EnvironmentVariables.Add("NEW_RELIC_ALLOW_ALL_HEADERS", "true");
                    fixture.EnvironmentVariables.Add("NEW_RELIC_ATTRIBUTES_EXCLUDE", "request.headers.referer, request.headers.accept, request.headers.cookie");
                }
            );

            fixture.Initialize();
        }
    }
}
