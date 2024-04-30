// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.Owin
{
    [NetFrameworkTest]
    public abstract class OwinWebApiTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : RemoteServiceFixtures.OwinWebApiFixture
    {
        private readonly RemoteServiceFixtures.OwinWebApiFixture _fixture;

        // The base test class runs tests for Owin 2; the derived classes test Owin 3 and 4
        protected OwinWebApiTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ForceTransactionTraces();
                    configModifier.AddAttributesInclude("request.parameters.*");
                    configModifier.SetLogLevel("debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetData();
                    _fixture.Get();
                    _fixture.Get404();
                    _fixture.GetId();
                    _fixture.Post();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
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

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSamples = _fixture.AgentLog.GetTransactionSamples();

            var getDataTransactionSample = transactionSamples
                .Where(sample => sample.Path == "WebTransaction/WebAPI/Values/Get")
                .FirstOrDefault();

            Assert.NotNull(getDataTransactionSample);

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

            // check the transaction trace sample
            var expectedTransactionTraceSegments = new List<string>
            {
                @"Owin Middleware Pipeline",
                @"DotNet/Values/Get"
            };

            var doNotExistTraceSegments = new List<string>
            {
                @"DotNet/Values/Get404",
                @"DotNet/Values/Post"
            };

            var expectedAttributes = new Dictionary<string, string>
            {
                { "request.parameters.data", "mything" },
                {"request.uri", "/api/values" }
            };

            NrAssert.Multiple(
                () => Assert.NotNull(getDataTransactionSample),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, getDataTransactionSample),
                () => Assertions.TransactionTraceSegmentsNotExist(doNotExistTraceSegments, getDataTransactionSample),
                () => Assertions.TransactionTraceHasAttributes(
                    expectedAttributes,
                    TransactionTraceAttributeType.Agent,
                    getDataTransactionSample
                )
            );
        }
    }

    public class OwinWebApiTests : OwinWebApiTestsBase<RemoteServiceFixtures.OwinWebApiFixture>
    {
        public OwinWebApiTests(RemoteServiceFixtures.OwinWebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin3WebApiTests : OwinWebApiTestsBase<RemoteServiceFixtures.Owin3WebApiFixture>
    {
        public Owin3WebApiTests(RemoteServiceFixtures.Owin3WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class Owin4WebApiTests : OwinWebApiTestsBase<RemoteServiceFixtures.Owin4WebApiFixture>
    {
        public Owin4WebApiTests(RemoteServiceFixtures.Owin4WebApiFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
