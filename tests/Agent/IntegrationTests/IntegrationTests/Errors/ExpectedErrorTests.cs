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

namespace NewRelic.Agent.IntegrationTests.Errors
{
    [NetCoreTest]
    public class ExpectedErrorTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public ExpectedErrorTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(15); // long enough to ensure metric harvest runs before error traces
                    configModifier.SetOrDeleteDistributedTraceEnabled(true);
                    configModifier.AddExpectedStatusCodes("410-450")
                    .AddExpectedErrorMessages("System.Exception", new List<string> { "test exception"})
                    .AddExpectedErrorClasses(new List<string> { "AspNetCoreMvcBasicRequestsApplication.Controllers.CustomExceptionClass" });
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.ReturnADesiredStatusCode(415);
                    _fixture.ThrowExceptionWithMessage("test exception message");
                    _fixture.ThrowCustomException();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ErrorTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assert.NotNull(metrics);

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"ErrorsExpected/all", CallCountAllHarvests = 3},
                new Assertions.ExpectedMetric { metricName = @"Supportability/Events/TransactionError/Seen", CallCountAllHarvests= 3 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/Events/TransactionError/Sent", CallCountAllHarvests = 3 },

            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // error metrics
                new Assertions.ExpectedMetric {metricName = @"Errors/all"},
                new Assertions.ExpectedMetric {metricName = @"Errors/allWeb"},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/MVC/ExpectedErrorTest/ThrowExceptionWithMessage/{exceptionMessage}"},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/MVC/ExpectedErrorTest/ThrowCustomException"},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/MVC/ExpectedErrorTest/ReturnADesiredStatusCode/{statusCode}"},
            };


            var expectedErrorAttributes = new Dictionary<string, string>
            {
                { "error.expected", "true"}
            };

            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.True(errorTraces.Count == 3, $"Expected 3 error traces but found {errorTraces.Count}"),
                () => Assert.True(errorEvents.Count == 3, $"Expected 3 error events but found {errorEvents.Count}")
            );

            foreach(var errorEvent in errorEvents)
            {
                Assertions.ErrorEventHasAttributes(expectedErrorAttributes, EventAttributeType.Intrinsic, errorEvent);
            }

            foreach (var errorTrace in errorTraces)
            {
                Assertions.ErrorTraceHasAttributes(expectedErrorAttributes, ErrorTraceAttributeType.Intrinsic, errorTrace);
            }


            var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();
            var errorSpanEvents = spanEvents.Where(se => se.AgentAttributes.ContainsKey("error.expected"));
            Assert.True(errorSpanEvents.Count() > 0);
        }
    }
}
