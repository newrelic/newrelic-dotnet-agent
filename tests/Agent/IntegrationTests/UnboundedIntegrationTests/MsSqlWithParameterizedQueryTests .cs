using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
    public class MsSqlWithParameterizedQuery : IClassFixture<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.MsSqlBasicMvcFixture _fixture;

        public MsSqlWithParameterizedQuery(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMsSql_WithParameterizedQuery();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allWeb", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{MsSqlConfiguration.MsSqlServer}/default", callCount = 4},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery"},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery"},
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 4, metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery"}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther"},

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", metricScope = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery" }
            };
            var expectedTransactionTraceSegments = new List<String>
            {
                $"Datastore/statement/MSSQL/teammembers/select"
            };

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan" },
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = MsSqlConfiguration.MsSqlServer},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"}
            };

            var expectedTransactionEventIntrinsicAttributes = new List<String>
            {
                "databaseDuration"
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery",
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery",
                    Sql = $"SELECT COUNT(*) FROM {_fixture.TableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/select",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery",
                    Sql = $"INSERT INTO {_fixture.TableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/insert",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery",
                    Sql = $"DELETE FROM {_fixture.TableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/delete",

                    HasExplainPlan = true
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/MsSql_WithParameterizedQuery");
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

                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }
}
