using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class BasicWebApplication : IClassFixture<RemoteServiceFixtures.BasicWebApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicWebApplication _fixture;

        public BasicWebApplication([NotNull] RemoteServiceFixtures.BasicWebApplication fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/default.aspx", metricScope = "WebTransaction/ASP/default.aspx", callCount = 1},
            };

            var expectedTransactionTraceSegments = new List<String>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
                @"default.aspx",
                @"ReleaseRequestState",
                @"UpdateRequestCache",
                @"EndRequest",
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/ASP/default.aspx")
                .FirstOrDefault();
            Assert.NotNull(transactionSample);

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );
        }
    }
}
