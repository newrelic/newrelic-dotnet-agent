using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class WcfAppSelfHosted : IClassFixture<RemoteServiceFixtures.WcfAppSelfHosted>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.WcfAppSelfHosted _fixture;

        public WcfAppSelfHosted([NotNull] RemoteServiceFixtures.WcfAppSelfHosted fixture, [NotNull] ITestOutputHelper testLogger)
        {
            _fixture = fixture;
            _fixture.TestLogger = testLogger;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.GetString();
                }
                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"HttpDispatcher", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"ApdexAll" },
                new Assertions.ExpectedMetric { metricName = @"Apdex" },
                new Assertions.ExpectedMetric { metricName = @"Apdex/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString" },
                new Assertions.ExpectedMetric { metricName = @"DotNet/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString", metricScope = @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString"},
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all" },
                new Assertions.ExpectedMetric { metricName = @"ApdexOther" },
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all" },
            };

            var expectedAttributes = new Dictionary<String, String>
            {
                { "custom key", "custom value" },
                { "custom foo", "custom bar" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .Where(sample => sample.Path == @"WebTransaction/WCF/NewRelic.Agent.IntegrationTests.Applications.WcfAppSelfHosted.IWcfService.GetString")
                .FirstOrDefault();
            Assert.True(transactionSample != null, "No transaction sample found.");

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.User, transactionSample)
            );
        }
    }
}
