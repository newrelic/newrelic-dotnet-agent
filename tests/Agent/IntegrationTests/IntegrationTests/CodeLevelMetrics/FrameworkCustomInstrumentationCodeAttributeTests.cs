// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CodeLevelMetrics
{
    [NetFrameworkTest]
    public class FrameworkCustomInstrumentationCodeAttributeTests : NewRelicIntegrationTest<RemoteServiceFixtures.AgentApiExecutor>
    {
        private readonly RemoteServiceFixtures.AgentApiExecutor _fixture;

        private const string ProgramNamespace = "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.Program";

        public FrameworkCustomInstrumentationCodeAttributeTests(RemoteServiceFixtures.AgentApiExecutor fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var instrumentationFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "CustomInstrumentation.xml");

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor", ProgramNamespace, "RealMain", "NewRelic.Providers.Wrapper.CustomInstrumentation.OtherTransactionWrapper", "MyCustomMetricName");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor", ProgramNamespace, "SomeSlowMethod", "NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory");

                    // Use the default wrapper
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor", ProgramNamespace, "SomeOtherMethod");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

            var myCustomMetricNameSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString() == "MyCustomMetricName");
            var someSlowMethodSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString().Contains("SomeSlowMethod"));
            var someOtherMethodSpan = spanEvents.FirstOrDefault(se => se.IntrinsicAttributes["name"].ToString().Contains("SomeOtherMethod"));

            Assert.NotNull(myCustomMetricNameSpan);
            Assert.NotNull(someSlowMethodSpan);
            Assert.NotNull(someOtherMethodSpan);

            NrAssert.Multiple
            (
                () => Assertions.SpanEventHasAttributes(_expectedMyCustomMetricNameAttributes, SpanEventAttributeType.Agent, myCustomMetricNameSpan),
                () => Assertions.SpanEventHasAttributes(_expectedSomeSlowMethodAttributes, SpanEventAttributeType.Agent, someSlowMethodSpan),
                () => Assertions.SpanEventHasAttributes(_expectedSomeOtherMethodAttributes, SpanEventAttributeType.Agent, someOtherMethodSpan)
            );
        }

        private readonly Dictionary<string, string> _expectedMyCustomMetricNameAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", ProgramNamespace },
            { "code.function", "RealMain" }
        };

        private readonly Dictionary<string, string> _expectedSomeSlowMethodAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", ProgramNamespace },
            { "code.function", "SomeSlowMethod" }
        };

        private readonly Dictionary<string, string> _expectedSomeOtherMethodAttributes = new Dictionary<string, string>()
        {
            { "code.namespace", ProgramNamespace },
            { "code.function", "SomeOtherMethod" }
        };

    }
}
