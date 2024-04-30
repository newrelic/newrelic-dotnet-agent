// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CodeLevelMetrics
{
    [NetFrameworkTest]
    public class FrameworkAspNetMvcCodeAttributeTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {

        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public FrameworkAspNetMvcCodeAttributeTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.GetWithAsyncDisabled();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void AsyncDisabledControllerTest()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            const string spanName = "DotNet/DisableAsyncSupportController/Index";
            Assert.Contains(spanEvents, x => x.IntrinsicAttributes["name"].ToString() == spanName);
            var spanEvent = spanEvents.FirstOrDefault(x => x.IntrinsicAttributes["name"].ToString() == spanName);
            Assert.NotNull(spanEvent);

            Assertions.SpanEventHasAttributes(new Dictionary<string, string>{
                { "code.namespace", "BasicMvcApplication.Controllers.DisableAsyncSupportController" },
                { "code.function", "Index" }
            }, SpanEventAttributeType.Agent, spanEvent);
        }

        [Fact]
        public void DefaultControllerTest()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            const string spanName = "DotNet/DefaultController/Index";
            Assert.Contains(spanEvents, x => x.IntrinsicAttributes["name"].ToString() == spanName);
            var spanEvent = spanEvents.FirstOrDefault(x => x.IntrinsicAttributes["name"].ToString() == spanName);
            Assert.NotNull(spanEvent);

            Assertions.SpanEventHasAttributes(new Dictionary<string, string>{
                { "code.namespace", "BasicMvcApplication.Controllers.DefaultController" },
                { "code.function", "Index" }
            }, SpanEventAttributeType.Agent, spanEvent);
        }
    }
}
