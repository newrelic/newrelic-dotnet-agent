// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.RequestHandling
{
    public abstract class NotFoundAndOptionsTests<T> : NewRelicIntegrationTest<T> where T : RemoteApplicationFixture
    {
        protected readonly T _fixture;

        public NotFoundAndOptionsTests(T fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: SetupApplication,
                exerciseApplication: ExerciseApplication
            );
            _fixture.Initialize();
        }

        protected virtual void SetupApplication() { }

        protected abstract void ExerciseApplication();

        [Fact]
        public void Test()
        {
            var expectedTransaction = @"WebTransaction/StatusCode/404";

            var unexpectedTransactions = new[]
            {
                @"WebTransaction/MVC/DefaultController/MissingAction",
                @"WebTransaction/MVC/MissingController",
                @"WebTransaction/ASP/{controller}/{action}/{id}"
            };

            var metrics = _fixture.AgentLog.GetMetrics();

            NrAssert.Multiple
            (
                () => Assertions.MetricExists(new Assertions.ExpectedMetric { metricName = expectedTransaction, CallCountAllHarvests = 2 }, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedTransactions.Select(t => new Assertions.ExpectedMetric { metricName = t }), metrics)
            );

            var badTransactionSamples = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path != expectedTransaction);

            var badTransactionEvents = _fixture.AgentLog.GetTransactionEvents()
                .Where(te => !te.IntrinsicAttributes["name"].Equals(expectedTransaction));

            NrAssert.Multiple
            (
                () => Assert.Empty(badTransactionSamples),
                () => Assert.Empty(badTransactionEvents)
            );
        }
    }
}
