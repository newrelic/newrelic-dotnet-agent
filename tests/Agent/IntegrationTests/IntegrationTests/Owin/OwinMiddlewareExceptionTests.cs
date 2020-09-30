// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public class OwinMiddlewareExceptionTests : IClassFixture<RemoteServiceFixtures.OwinWebApiFixture>
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        public OwinMiddlewareExceptionTests(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.InvokeBadMiddleware();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/Owin Middleware Pipeline", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/all", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/allWeb", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Errors/WebTransaction/StatusCode/500", callCount = 1},
            };

            var expectedExceptionClassName = "System.ArgumentException";
            var expectedExceptionMessage = "This exception is from the BadMiddleware";

            var expectedTransactionEventAttributes = new Dictionary<string, string>
            {
                { "errorType", expectedExceptionClassName },
                { "errorMessage", expectedExceptionMessage },
            };

            var expectedErrorEventAttributes = new Dictionary<string, string>
            {
                { "error.class", expectedExceptionClassName },
                { "error.message", expectedExceptionMessage },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var errorTraces = _fixture.AgentLog.GetErrorTraces().ToList();
            var errorEvents = _fixture.AgentLog.GetErrorEvents().ToList();
            var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();


            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.NotEmpty(errorTraces),
                () => Assert.NotEmpty(_fixture.AgentLog.GetErrorEvents()),
                () => Assert.Equal("WebTransaction/StatusCode/500", errorTraces[0].Path),
                () => Assert.Equal(expectedExceptionClassName, errorTraces[0].ExceptionClassName),
                () => Assert.Equal(expectedExceptionMessage, errorTraces[0].Message),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAttributes, TransactionEventAttributeType.Intrinsic, transactionEvents[0]),
                () => Assertions.ErrorEventHasAttributes(expectedErrorEventAttributes, EventAttributeType.Intrinsic, errorEvents[0].Events[0])
            );
        }
    }

    public class Owin3MiddlewareExceptionTests : OwinMiddlewareExceptionTests, IClassFixture<RemoteServiceFixtures.Owin3WebApiFixture>
    {
        public Owin3MiddlewareExceptionTests(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    public class Owin4MiddlewareExceptionTests : OwinMiddlewareExceptionTests, IClassFixture<RemoteServiceFixtures.Owin4WebApiFixture>
    {
        public Owin4MiddlewareExceptionTests(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
