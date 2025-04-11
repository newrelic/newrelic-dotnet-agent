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
    public class CustomInstrumentationEditorAgentCommand : NewRelicIntegrationTest<MvcWithCollectorFixture>
    {
        private readonly MvcWithCollectorFixture _fixture;

        public CustomInstrumentationEditorAgentCommand(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.TriggerCustomInstrumentationEditorAgentCommand();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationUpdateCommandLogLineRegex, TimeSpan.FromMinutes(3));

                    _fixture.AgentLog.ClearLog(TimeSpan.FromSeconds(5));
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
