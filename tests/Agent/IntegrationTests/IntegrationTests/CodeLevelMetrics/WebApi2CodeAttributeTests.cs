// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CodeLevelMetrics
{
    [NetFrameworkTest]
    public class WebApi2CodeAttributeTests : NewRelicIntegrationTest<WebApiAsyncFixture>
    {
        private readonly WebApiAsyncFixture _fixture;
        
        private const string AsyncAwaitControllerNamespace = "WebApiAsyncApplication.Controllers.AsyncAwaitController";

        public WebApi2CodeAttributeTests(WebApiAsyncFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.GetIoBoundNoSpecialAsync();
                    _fixture.GetIoBoundConfigureAwaitFalseAsync();
                    _fixture.GetCpuBoundTasksAsync();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            var getIoBoundNoSpecialSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/AsyncAwait/IoBoundNoSpecialAsync");
            var getIoBoundConfigureAwaitFalseSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/AsyncAwait/IoBoundConfigureAwaitFalseAsync");
            var getCpuBoundSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/AsyncAwait/CpuBoundTasksAsync");

            Assert.NotNull(getIoBoundNoSpecialSpan);
            Assert.NotNull(getIoBoundConfigureAwaitFalseSpan);
            Assert.NotNull(getCpuBoundSpan);

            NrAssert.Multiple
            (
                () => Assertions.SpanEventHasAttributes(_expectedGetIoBoundNoSpecialAsyncAttributes, SpanEventAttributeType.Agent, getIoBoundNoSpecialSpan),
                () => Assertions.SpanEventHasAttributes(_expectedGetIoBoundConfigureAwaitFalseAsyncAttributes, SpanEventAttributeType.Agent, getIoBoundConfigureAwaitFalseSpan),
                () => Assertions.SpanEventHasAttributes(_expectedGetCpuBoundTasksAsyncAttributes, SpanEventAttributeType.Agent, getCpuBoundSpan)
            );
        }

        private readonly Dictionary<string, string> _expectedGetIoBoundNoSpecialAsyncAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncAwaitControllerNamespace },
            { "code.function", "IoBoundNoSpecialAsync" }
        };

        private readonly Dictionary<string, string> _expectedGetIoBoundConfigureAwaitFalseAsyncAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncAwaitControllerNamespace },
            { "code.function", "IoBoundConfigureAwaitFalseAsync" }
        };

        private readonly Dictionary<string, string> _expectedGetCpuBoundTasksAsyncAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncAwaitControllerNamespace },
            { "code.function", "CpuBoundTasksAsync" }
        };


    }
}
