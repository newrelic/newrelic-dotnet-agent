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
    public class PostgresTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public PostgresTests([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "NewRelic.Agent.Core.Tracer.Factories.Sql.DataReaderTracerFactory", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetPostgres();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// The Npgsql driver executes an unrelated SELECT query while connecting
				new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{PostgresConfiguration.PostgresServer}/{PostgresConfiguration.PostgresPort}", callCount = 2},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/Postgres"},

				// We don't currently support NpgsqlDataReader
				//new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 3 },
				//new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 3, metricScope = "WebTransaction/MVC/DefaultController/Postgres"}
			};
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// The datastore operation happened inside a web transaction so there should be no allOther metrics
				new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", callCount = 1 },

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/Postgres" }
            };
            var expectedTransactionTraceSegments = new List<String>
            {
                "Datastore/statement/Postgres/teammembers/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<String>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/Postgres",
                    Sql = "SELECT * FROM newrelic.teammembers WHERE firstname = ?",
                    DatastoreMetricName = "Datastore/statement/Postgres/teammembers/select",
                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/Postgres");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/Postgres");
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }
}
