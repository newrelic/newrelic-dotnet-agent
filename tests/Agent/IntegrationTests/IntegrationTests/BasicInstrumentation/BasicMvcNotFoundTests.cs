// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
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
            var good404 = @"WebTransaction/StatusCode/404";

            var bad404 = new[]
            {
                @"WebTransaction/MVC/DefaultController/MissingAction",
                @"WebTransaction/ASP/{controller}/{action}/{id}"
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            NrAssert.Multiple
            (
                () => Assertions.MetricExist(new Assertions.ExpectedMetric { metricName = good404, callCount = 2 }, metrics),
                () => Assertions.MetricsDoNotExist(bad404.Select(m => new Assertions.ExpectedMetric { metricName = m }), metrics)
            );

            var badTransactionSamples = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path != good404);

            var badTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(te => !te.IntrinsicAttributes["name"].Equals(good404));

            NrAssert.Multiple
            (
                () => Assert.Empty(badTransactionSamples),
                () => Assert.Empty(badTransactionEvents)
            );
        }
    }
}
