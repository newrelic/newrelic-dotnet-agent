// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Oracle
{
    [NetFrameworkTest]
    public class OracleTests : NewRelicIntegrationTest<RemoteServiceFixtures.OracleBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.OracleBasicMvcFixture _fixture;

        public OracleTests(RemoteServiceFixtures.OracleBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(15);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetOracle();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedDatastoreCallCount = 4; // SELECT, INSERT, COUNT, and DELETE from GetOracle() above

            //This value is dictated by the query that is being run as part of this test. In this case, we're running a query that returns a single row.
            //This results in two calls to read. Therefore the call count for the Iterate metric should be 2.
            var expectedIterateCallCount = 2;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Oracle/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Oracle/allWeb", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Oracle/{OracleConfiguration.OracleServer}/{OracleConfiguration.OraclePort}", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Oracle/user_tables/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Oracle/user_tables/select", callCount = 1, metricScope = "WebTransaction/MVC/OracleController/Oracle"},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/select", callCount = 1, metricScope = "WebTransaction/MVC/OracleController/Oracle"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/insert", callCount = 1, metricScope = "WebTransaction/MVC/OracleController/Oracle"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Oracle/{_fixture.TableName}/delete", callCount = 1, metricScope = "WebTransaction/MVC/OracleController/Oracle"},

                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate" , callCount = expectedIterateCallCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount, metricScope = "WebTransaction/MVC/OracleController/Oracle"}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Oracle/allOther"},

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/select", metricScope = "WebTransaction/MVC/OracleController/Oracle" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/insert", metricScope = "WebTransaction/MVC/OracleController/Oracle" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Oracle/delete", metricScope = "WebTransaction/MVC/OracleController/Oracle" }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/Oracle/user_tables/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/OracleController/Oracle",
                    Sql = "SELECT DEGREE FROM user_tables WHERE ROWNUM <= ?",
                    DatastoreMetricName = "Datastore/statement/Oracle/user_tables/select",
                    HasExplainPlan = false
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/OracleController/Oracle",
                    Sql = $"SELECT COUNT(*) FROM {_fixture.TableName}",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_fixture.TableName}/select",

                    HasExplainPlan = false
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/OracleController/Oracle",
                    Sql = $"INSERT INTO {_fixture.TableName} (HOTEL_ID, BOOKING_DATE) VALUES (?, SYSDATE)",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_fixture.TableName}/insert",

                    HasExplainPlan = false
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/OracleController/Oracle",
                    Sql = $"DELETE FROM {_fixture.TableName} WHERE HOTEL_ID = ?",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_fixture.TableName}/delete",

                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/OracleController/Oracle");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/OracleController/Oracle");
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
