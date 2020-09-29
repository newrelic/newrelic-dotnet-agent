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
    public class OwinWebApiTests : IClassFixture<RemoteServiceFixtures.OwinWebApiFixture>
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        public OwinWebApiTests(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
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
                    _fixture.GetData();
                    _fixture.Get();
                    _fixture.Get404();
                    _fixture.GetId();
                    _fixture.Post();

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
                new Assertions.ExpectedMetric {metricName = @"DotNet/Owin Middleware Pipeline", callCount = 5},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = 5},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebAPI/Values/Get", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebAPI/Values/Post", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebAPI/Values/Get404", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/Values/Get", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/Values/Post", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/Values/Get404", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"External/all", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"External/allWeb", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"External/www.google.com/all", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"External/www.google.com/Stream/GET", callCount = 1}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/all", callCount = 5},
            };

            var expectedAttributes = new Dictionary<string, string>
            {
                 { "request.parameters.data", "mything" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSamples = _fixture.AgentLog.GetTransactionSamples();
            //this is the transaction trace that is generally returned, but this 
            //is not necessarily always the case
            var getTransactionSample = transactionSamples
                .Where(sample => sample.Path == "WebTransaction/WebAPI/Values/Get")
                .FirstOrDefault();
            var get404TransactionSample = transactionSamples
                .Where(sample => sample.Path == "WebTransaction/WebAPI/Values/Get404")
                .FirstOrDefault();
            var postTransactionSample = transactionSamples
                .Where(sample => sample.Path == "WebTransaction/WebAPI/Values/Post")
                .FirstOrDefault();

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.NotNull(transactionEventWithExternal),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );

            // check the transaction trace samples
            TransactionSample traceToCheck = null;
            List<string> expectedTransactionTraceSegments = null;
            List<string> doNotExistTraceSegments = null;
            if (getTransactionSample != null)
            {
                traceToCheck = getTransactionSample;
                expectedTransactionTraceSegments = new List<string>
                {
                    @"Owin Middleware Pipeline",
                    @"DotNet/Values/Get"
                };
                doNotExistTraceSegments = new List<string>
                {
                    @"DotNet/Values/Get404",
                    @"DotNet/Values/Post"
                };
                expectedAttributes.Add("request.uri", "/api/values");
            }
            else if (get404TransactionSample != null)
            {
                traceToCheck = get404TransactionSample;
                expectedTransactionTraceSegments = new List<string>
                {
                    @"Owin Middleware Pipeline",
                    @"DotNet/Values/Get404"
                };
                doNotExistTraceSegments = new List<string>
                {
                    @"External/www.google.com/Stream/GET",
                    @"DotNet/Values/Get",
                    @"DotNet/Values/Post"
                };
                expectedAttributes.Add("request.uri", "/api/404");
            }
            else if (postTransactionSample != null)
            {
                traceToCheck = postTransactionSample;
                expectedTransactionTraceSegments = new List<string>
                {
                    @"Owin Middleware Pipeline",
                    @"DotNet/Values/Post"
                };
                doNotExistTraceSegments = new List<string>
                {
                    @"External/www.google.com/Stream/GET",
                    @"DotNet/Values/Get404",
                    @"DotNet/Values/Get"
                };
                expectedAttributes.Add("request.uri", "/api/values");
            }

            NrAssert.Multiple(
                () => Assert.NotNull(traceToCheck),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, traceToCheck),
                () => Assertions.TransactionTraceSegmentsNotExist(doNotExistTraceSegments, traceToCheck),
                () => Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent,
                    traceToCheck));
        }
    }

    public class Owin3WebApiTests : OwinWebApiTests, IClassFixture<RemoteServiceFixtures.Owin3WebApiFixture>
    {
        public Owin3WebApiTests(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    public class Owin4WebApiTests : OwinWebApiTests, IClassFixture<RemoteServiceFixtures.Owin4WebApiFixture>
    {
        public Owin4WebApiTests(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
