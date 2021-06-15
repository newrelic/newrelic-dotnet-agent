// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class BasicMvcNotFoundTests : NewRelicIntegrationTest<RemoteServiceFixtures.BasicMvcApplicationTestFixture>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplicationTestFixture _fixture;

        public BasicMvcNotFoundTests(RemoteServiceFixtures.BasicMvcApplicationTestFixture fixture, ITestOutputHelper output)
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
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.Get404("Default/MissingAction");
                    _fixture.Get404("MissingController");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsSeen", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/AnalyticsEvents/TotalEventsCollected", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/StatusCode/404", callCount = 2 }
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/DefaultController/MissingAction" },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/{controller}/{action}/{id}" }
            };

            var connect = _fixture.AgentLog.GetConnectData().Environment.GetPluginList();
            Assert.DoesNotContain(connect, x => x.Contains("NewRelic.Providers.Wrapper.AspNetCore"));

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var badTransactionSamples = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path != @"WebTransaction/StatusCode/404");

            var badTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(te => !te.IntrinsicAttributes["name"].Equals(@"WebTransaction/StatusCode/404"));

            NrAssert.Multiple
            (
                () => Assert.Empty(badTransactionSamples),
                () => Assert.Empty(badTransactionEvents)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }
}
