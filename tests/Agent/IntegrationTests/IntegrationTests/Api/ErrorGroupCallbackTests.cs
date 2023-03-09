// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Api
{
    public abstract class ErrorGroupCallbackTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string ErrorGroupName = "error.group.name";
        private string _errorGroupValue;
        private string _testCommand;

        protected readonly TFixture Fixture;

        public ErrorGroupCallbackTestsBase(TFixture fixture, ITestOutputHelper output, string testCommand, string testParameter) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;
            _testCommand = testCommand;

            _errorGroupValue = testParameter;
            if (string.IsNullOrWhiteSpace(testParameter))
            {
                Fixture.AddCommand($"ApiCalls {testCommand}");
            }
            else
            {
                Fixture.AddCommand($"ApiCalls {testCommand} {testParameter}");
            }

            Fixture.AddCommand("AttributeInstrumentation MakeWebTransactionWithException");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(Fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.SetLogLevel("finest");
                    configModifier.AddExpectedErrorMessages("System.Exception", new List<string> { "Test Message" });
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
                new Assertions.ExpectedMetric(){ callCount = 1, metricName = "Supportability/ApiInvocation/SetErrorGroupCallback"}
            };

            var asserts = new List<Action> { () => Assertions.MetricsExist(apiMetrics, actualMetrics) };
            if (_testCommand == "TestSetErrorGroupCallbackReturnsString")
            {
                StringTests(asserts, errorEvent, errorTrace);
            }
            else if (_testCommand == "TestSetErrorGroupCallbackWithKeys")
            {
                DictionaryTests(asserts, errorEvent, errorTrace);
            }
            else if (_testCommand == "TestNullSetErrorGroupCallback")
            {
                NullTests(asserts, errorEvent, errorTrace);
            }
            else
            {
                Assert.True(false, "Test Command was invalid: " + _testCommand);
            }

            NrAssert.Multiple(asserts.ToArray());
        }

        private void StringTests(List<Action> asserts, ErrorEventEvents errorEvent, ErrorTrace errorTrace)
        {
            var expectedAgentAttributes = new Dictionary<string, string>
            {
                { ErrorGroupName, _errorGroupValue}
            };

            asserts.Add(() => Assertions.ErrorEventHasAttributes(expectedAgentAttributes, EventAttributeType.Agent, errorEvent));
            asserts.Add(() => Assertions.ErrorTraceHasAttributes(expectedAgentAttributes, ErrorTraceAttributeType.Agent, errorTrace));
        }

        private void DictionaryTests(List<Action> asserts, ErrorEventEvents errorEvent, ErrorTrace errorTrace)
        {
            var expectedAgentAttributes = new Dictionary<string, string>
            {
                { ErrorGroupName, "success"}
            };

            asserts.Add(() => Assertions.ErrorEventHasAttributes(expectedAgentAttributes, EventAttributeType.Agent, errorEvent));
            asserts.Add(() => Assertions.ErrorTraceHasAttributes(expectedAgentAttributes, ErrorTraceAttributeType.Agent, errorTrace));
        }

        private void NullTests(List<Action> asserts, ErrorEventEvents errorEvent, ErrorTrace errorTrace)
        {
            var unexpectedAgentAttributes = new List<string> { ErrorGroupName };

            asserts.Add(() => Assertions.ErrorEventDoesNotHaveAttributes(unexpectedAgentAttributes, EventAttributeType.Agent, errorEvent));
            asserts.Add(() => Assertions.ErrorTraceDoesNotHaveAttributes(unexpectedAgentAttributes, ErrorTraceAttributeType.Agent, errorTrace));
        }
    }

    [NetFrameworkTest]
    public class ErrorGroupCallbackReturnsStringTestsFW : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ErrorGroupCallbackReturnsStringTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestSetErrorGroupCallbackReturnsString", "CustomErrorGroup")
        {
        }
    }

    [NetFrameworkTest]
    public class ErrorGroupCallbackWithKeyTestsFW : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ErrorGroupCallbackWithKeyTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestSetErrorGroupCallbackWithKeys", string.Empty)
        {
        }
    }

    [NetFrameworkTest]
    public class ErrorGroupCallbackNullTestsFW : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ErrorGroupCallbackNullTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestNullSetErrorGroupCallback",  string.Empty)
        {
        }
    }

    [NetCoreTest]
    public class ErrorGroupCallbackReturnsStringTestsCore : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ErrorGroupCallbackReturnsStringTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestSetErrorGroupCallbackReturnsString", "CustomErrorGroup")
        {
        }
    }

    [NetCoreTest]
    public class ErrorGroupCallbackWithKeyTestsCore : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ErrorGroupCallbackWithKeyTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestSetErrorGroupCallbackWithKeys", string.Empty)
        {
        }
    }

    [NetCoreTest]
    public class ErrorGroupCallbackNullTestsCore : ErrorGroupCallbackTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ErrorGroupCallbackNullTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "TestNullSetErrorGroupCallback", string.Empty)
        {
        }
    }
}
