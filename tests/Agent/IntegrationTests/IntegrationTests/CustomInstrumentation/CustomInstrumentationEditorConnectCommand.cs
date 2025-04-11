// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Testing.Assertions;


namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    public class CustomInstrumentationEditorConnectCommand : NewRelicIntegrationTest<MvcWithCollectorFixture>
    {
        private readonly MvcWithCollectorFixture _fixture;

        public CustomInstrumentationEditorConnectCommand(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                    configModifier.SetLogLevel("finest");
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.SetCustomInstrumentationEditorOnConnect();
                    _fixture.Get();
                    _fixture.StartAgent();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.GenerateCallsToCustomInstrumentationEditorMethods();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"Custom/Live/CustomMethodDefaultTracer", callCount = 1 },
				
				// Scoped
				new Assertions.ExpectedMetric { metricName = @"Custom/Live/CustomMethodDefaultTracer", metricScope = "WebTransaction/MVC/CustomInstrumentationController/Get", callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}

