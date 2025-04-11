// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvcFrameworkTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcFrameworkFixture>
    {

        private readonly RemoteServiceFixtures.AspNetCoreMvcFrameworkFixture _fixture;

        public AspNetCoreMvcFrameworkTests(RemoteServiceFixtures.AspNetCoreMvcFrameworkFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.UseLocalConfig = true;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                },
                exerciseApplication: () =>
                {
                    _fixture.GetCORSPreflight();
                    _fixture.Get();
                    //We need to wait for a harvest cycle otherwise we do not always have enough time for the transaction
                    //to complete and have all of its data included in the forced harvest during shutdown.
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Apdex"},
                new Assertions.ExpectedMetric { metricName = @"ApdexAll"},
                new Assertions.ExpectedMetric { metricName = @"Apdex/MVC/Home/Index"},
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransactionTotalTime/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/Middleware Pipeline", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = @"WebTransaction/MVC/Home/Index", callCount = 1 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// SUPNET-492 (ASP.NET Core CORS MGI)
				new Assertions.ExpectedMetric {metricName = @"WebTransaction/ASP/Home/About", callCount = 1},
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"Middleware Pipeline",
                @"DotNet/HomeController/Index"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().Where(sample => sample.Path == @"WebTransaction/MVC/Home/Index")
                .FirstOrDefault();

            Assert.NotNull(metrics);

            Assert.NotNull(transactionSample);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
