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
    public class MsSqlAsyncTests : IClassFixture<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.MsSqlBasicMvcFixture _fixture;

        public MsSqlAsyncTests(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
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

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMsSqlAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{MsSqlConfiguration.MsSqlServer}/default", callCount = 5},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync"},
                //ExecuteScalarAsync is double-instrumented: DOTNET-1800
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/select", callCount = 2, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/insert", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.TableName}/delete", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync"},
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 3, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync"}
                //ExecuteScalarAsync loses shared thread context while calling datareader.read()... so have one less scoped.
                //reader.ReadAsync(cancellationToken).ContinueWith(delegate(Task<bool> readTask)
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther", callCount = 5 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 3, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MsSqlAsync" }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MSSQL/teammembers/select"
            };
            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = MsSqlConfiguration.MsSqlServer},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"}

            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSqlAsync",
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSqlAsync",
                    Sql = $"SELECT COUNT(*) FROM {_fixture.TableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/select",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSqlAsync",
                    Sql = $"INSERT INTO {_fixture.TableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/insert",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MsSqlAsync",
                    Sql = $"DELETE FROM {_fixture.TableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.TableName}/delete",

                    HasExplainPlan = true
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/MsSqlAsync");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/MsSqlAsync");
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
