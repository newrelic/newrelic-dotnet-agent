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

namespace NewRelic.Agent.IntegrationTests.CodeLevelMetrics
{
    [NetCoreTest]
    public class AspNetCoreMvcCodeAttributeTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreMvcCodeAttributeTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
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
                    _fixture.ThrowException();
                    _fixture.GetCallAsyncExternal();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            var getIndexSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/HomeController/Index");
            var throwExceptionSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/HomeController/ThrowException");
            var callAsyncExternalSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "DotNet/DetachWrapperController/CallAsyncExternal");

            Assert.NotNull(getIndexSpan);
            Assert.NotNull(throwExceptionSpan);
            Assert.NotNull(callAsyncExternalSpan);

            NrAssert.Multiple
            (
                () => Assertions.SpanEventHasAttributes(_expectedGetIndexAttributes, SpanEventAttributeType.Agent, getIndexSpan),
                () => Assertions.SpanEventHasAttributes(_expectedThrowExceptionAttributes, SpanEventAttributeType.Agent, throwExceptionSpan),
                () => Assertions.SpanEventHasAttributes(_expectedCallAsyncExternalAttributes, SpanEventAttributeType.Agent, callAsyncExternalSpan)
            );
        }

        private readonly Dictionary<string, string> _expectedGetIndexAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", "AspNetCoreMvcBasicRequestsApplication.Controllers.HomeController" },
            { "code.function", "Index" }
        };

        private readonly Dictionary<string, string> _expectedThrowExceptionAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", "AspNetCoreMvcBasicRequestsApplication.Controllers.HomeController" },
            { "code.function", "ThrowException" }
        };

        private readonly Dictionary<string, string> _expectedCallAsyncExternalAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", "AspNetCoreMvcBasicRequestsApplication.Controllers.DetachWrapperController" },
            { "code.function", "CallAsyncExternal" }
        };
    }
}
