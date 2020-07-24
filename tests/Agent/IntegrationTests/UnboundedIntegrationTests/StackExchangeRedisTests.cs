using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
    public class StackExchangeRedisTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public StackExchangeRedisTests([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
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

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "NewRelic.Agent.Core.Tracer.Factories.Sql.DataReaderTracerFactory", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetStackExchangeRedis();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Redis/allWeb", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Redis/{StackExchangeRedisConfiguration.StackExchangeRedisServer}/{StackExchangeRedisConfiguration.StackExchangeRedisPort}", callCount = 2},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/SET", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Redis/GET", callCount = 1 }
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric {metricName = @"Datastore/allOther", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Datastore/Redis/allOther", callCount = 1}
            };

            var expectedTransactionEventIntrinsicAttributes = new List<String>
            {
                "databaseDuration"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/StackExchangeRedis");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/StackExchangeRedis");

            NrAssert.Multiple
            (
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent)
            );
        }
    }
}
