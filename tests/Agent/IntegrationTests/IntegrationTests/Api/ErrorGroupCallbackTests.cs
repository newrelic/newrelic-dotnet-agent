// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    public abstract class ErrorGroupCallbackTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string ErrorGroupName = "error.group.name";
        private const string ErrorGroupValue = "TestErrors";

        protected readonly TFixture Fixture;

        public ErrorGroupCallbackTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand($"ApiCalls TestSetErrorGroupCallback");
            Fixture.AddCommand("AttributeInstrumentation MakeWebTransactionWithException");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(Fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetLogLevel("finest");
                    configModifier.AddExpectedErrorMessages("System.Exception", new List<string> { "Test Message" });
                    configModifier.EnableAgentTiming(true);
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var errorEvents = Fixture.AgentLog.GetErrorEvents();
            var errorEvent = errorEvents.FirstOrDefault();

            var errorTraces = Fixture.AgentLog.GetErrorTraces();
            var errorTrace = errorTraces.FirstOrDefault();

            var actualMetrics = Fixture.AgentLog.GetMetrics().ToList();

            var apiMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Supportability/ApiInvocation/SetErrorGroupCallback"},
                new Assertions.ExpectedMetric() { callCount = 1, metricName = "Supportability/AgentTiming/ErrorEventMakerSetErrorGroup" },
                new Assertions.ExpectedMetric() { callCount = 1, metricName = "Supportability/AgentTiming/ErrorTraceMakerSetErrorGroup" }
            };

            var expectedAgentAttributes = new Dictionary<string, string>
            {
                { ErrorGroupName, ErrorGroupValue}
            };
            var asserts = new List<Action> {  };

            NrAssert.Multiple(
                () => Assertions.MetricsExist(apiMetrics, actualMetrics),
                () => Assertions.ErrorEventHasAttributes(expectedAgentAttributes, EventAttributeType.Agent, errorEvent),
                () => Assertions.ErrorTraceHasAttributes(expectedAgentAttributes, ErrorTraceAttributeType.Agent, errorTrace)
            );
        }
    }

    [NetFrameworkTest]
    public class ErrorGroupCallbackReturnsStringTestsFW : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ErrorGroupCallbackReturnsStringTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ErrorGroupCallbackReturnsStringTestsCore : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ErrorGroupCallbackReturnsStringTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
