// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;
using System;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.CodeLevelMetrics
{
    [NetCoreTest]
    public class NetCoreCustomInstrumentationCodeAttributeTests : NewRelicIntegrationTest<NetCoreAsyncTestsFixture>
    {
        private readonly NetCoreAsyncTestsFixture _fixture;

        private const string AsyncUseCasesNamespace = "NetCoreAsyncApplication.AsyncUseCases";

        public NetCoreCustomInstrumentationCodeAttributeTests(NetCoreAsyncTestsFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);

                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", AsyncUseCasesNamespace, "IoBoundNoSpecialAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundNoSpecialAsync", 7);
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", AsyncUseCasesNamespace, "IoBoundConfigureAwaitFalseAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.OtherTransactionWrapperAsync", "IoBoundConfigureAwaitFalseAsync", 7);

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NetCoreAsyncApplication", AsyncUseCasesNamespace, "CustomMethodAsync1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "CustomMethodAsync1");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            var ioBoundNoSpecialSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "IoBoundNoSpecialAsync");
            var ioBoundConfigureAwaitFalseSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "IoBoundConfigureAwaitFalseAsync");
            var customMethodAsync1Span = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "CustomMethodAsync1");

            Assert.NotNull(ioBoundNoSpecialSpan);
            Assert.NotNull(ioBoundConfigureAwaitFalseSpan);
            Assert.NotNull(customMethodAsync1Span);

            NrAssert.Multiple
            (
                () => Assertions.SpanEventHasAttributes(_expectedIoBoundNoSpecialAttributes, SpanEventAttributeType.Agent, ioBoundNoSpecialSpan),
                () => Assertions.SpanEventHasAttributes(_expectedIoBoundConfigureAwaitFalseAttributes, SpanEventAttributeType.Agent, ioBoundConfigureAwaitFalseSpan),
                () => Assertions.SpanEventHasAttributes(_expectedCustomMethodAsync1Attributes, SpanEventAttributeType.Agent, customMethodAsync1Span)
            );
        }

        private readonly Dictionary<string, string> _expectedIoBoundNoSpecialAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncUseCasesNamespace },
            { "code.function", "IoBoundNoSpecialAsync" }
        };

        private readonly Dictionary<string, string> _expectedIoBoundConfigureAwaitFalseAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncUseCasesNamespace },
            { "code.function", "IoBoundConfigureAwaitFalseAsync" }
        };

        private readonly Dictionary<string, string> _expectedCustomMethodAsync1Attributes = new Dictionary<string, string>()
        {
            { "code.namespace", AsyncUseCasesNamespace },
            { "code.function", "CustomMethodAsync1" }
        };
    }
}
