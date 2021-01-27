// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// DB2 tests have a dependency on IBM software that is behind login-locked walls.
//using System.Collections.Generic;
//using System.Linq;
//using NewRelic.Agent.IntegrationTestHelpers;
//using NewRelic.Agent.IntegrationTestHelpers.Models;
//using NewRelic.Agent.IntegrationTests.Shared;
//using NewRelic.Testing.Assertions;
//using Xunit;
//using Xunit.Abstractions;

//namespace NewRelic.Agent.UnboundedIntegrationTests.IbmDb2
//{
//    public class IbmDb2Tests : IClassFixture<RemoteServiceFixtures.IbmDb2BasicMvcFixture>
//    {
//        private readonly RemoteServiceFixtures.IbmDb2BasicMvcFixture _fixture;

//        public IbmDb2Tests(RemoteServiceFixtures.IbmDb2BasicMvcFixture fixture, ITestOutputHelper output)
//        {
//            _fixture = fixture;
//            _fixture.TestLogger = output;
//            _fixture.Actions
//            (
//                setupConfiguration: () =>
//                {
//                    var configPath = fixture.DestinationNewRelicConfigFilePath;
//                    var configModifier = new NewRelicConfigModifier(configPath);

//                    configModifier.ForceTransactionTraces();

//                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

//                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
//                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
//                },
//                exerciseApplication: () =>
//                {
//                    _fixture.GetIbmDb2();
//                }
//            );
//            _fixture.Initialize();
//        }

//        [Fact]
//        public void Test()
//        {
//            var expectedMetrics = new List<Assertions.ExpectedMetric>
//            {
//                // The IBMDB2 driver executes an unrelated DECLARE query while connecting
//                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 4 },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 4 },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/IBMDB2/all", callCount = 4 },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/IBMDB2/allWeb", callCount = 4 },
//                new Assertions.ExpectedMetric { metricName = $"Datastore/instance/IBMDB2/{CommonUtils.NormalizeHostname(Db2Configuration.Db2Server)}/default", callCount = 4},
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/select", callCount = 2 },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/IBMDB2/employee/select", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/IBMDB2/employee/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query"},
//                //ExecuteScalar() double instrumented: DOTNET-1800
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/select", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query"},
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/insert", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/insert", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/insert", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query"},
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/delete", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/delete", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_fixture.TableName}/delete", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query"},

//                // We are not checking callCount on Iterate metrics because they can be confusing in that calls like Open can result in calls to Read.
//                // This is particularly true for MySQL, but doing this for all vendors for consistency.
//                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate" },
//                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query"}
//            };
//            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
//            {
//                // The datastore operation happened inside a web transaction so there should be no allOther metrics
//                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther" },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/IBMDB2/allOther"},

//                // The operation metric should not be scoped because the statement metric is scoped instead
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/select", metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query" },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/insert", metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query" },
//                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/IBMDB2/delete", metricScope = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query" }
//            };
//            var expectedTransactionTraceSegments = new List<string>
//            {
//                "Datastore/statement/IBMDB2/employee/select"
//            };

//            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
//            {
//                new Assertions.ExpectedSegmentParameter { segmentName = @"Datastore/statement/IBMDB2/employee/select", parameterName = "host", parameterValue = $"{CommonUtils.NormalizeHostname(Db2Configuration.Db2Server)}"},
//                new Assertions.ExpectedSegmentParameter { segmentName = @"Datastore/statement/IBMDB2/employee/select", parameterName = "port_path_or_id", parameterValue = "default"},
//                new Assertions.ExpectedSegmentParameter { segmentName = @"Datastore/statement/IBMDB2/employee/select", parameterName = "database_name", parameterValue = "SAMPLE"}

//            };

//            var expectedTransactionEventIntrinsicAttributes = new List<string>
//            {
//                "databaseDuration"
//            };
//            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
//            {
//                new Assertions.ExpectedSqlTrace
//                {
//                    TransactionName = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query",
//                    Sql = "SELECT LASTNAME FROM EMPLOYEE FETCH FIRST ROW ONLY",
//                    DatastoreMetricName = "Datastore/statement/IBMDB2/employee/select",
//                    HasExplainPlan = false
//                },
//                new Assertions.ExpectedSqlTrace
//                {
//                    TransactionName = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query",
//                    Sql = $"SELECT COUNT(*) FROM {_fixture.TableName}",
//                    DatastoreMetricName = $"Datastore/statement/IBMDB2/{_fixture.TableName}/select",

//                    HasExplainPlan = false
//                },
//                new Assertions.ExpectedSqlTrace
//                {
//                    TransactionName = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query",
//                    Sql = $"INSERT INTO {_fixture.TableName} (HOTEL_ID, BOOKING_DATE) VALUES (?, SYSDATE)",
//                    DatastoreMetricName = $"Datastore/statement/IBMDB2/{_fixture.TableName}/insert",

//                    HasExplainPlan = false
//                },
//                new Assertions.ExpectedSqlTrace
//                {
//                    TransactionName = "WebTransaction/MVC/DefaultController/InvokeIbmDb2Query",
//                    Sql = $"DELETE FROM {_fixture.TableName} WHERE HOTEL_ID = ?",
//                    DatastoreMetricName = $"Datastore/statement/IBMDB2/{_fixture.TableName}/delete",

//                    HasExplainPlan = false
//                }
//            };

//            var metrics = _fixture.AgentLog.GetMetrics().ToList();
//            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/InvokeIbmDb2Query");
//            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/InvokeIbmDb2Query");
//            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

//            NrAssert.Multiple(
//                () => Assert.NotNull(transactionSample),
//                () => Assert.NotNull(transactionEvent)
//                );

//            NrAssert.Multiple
//            (
//                () => Assertions.MetricsExist(expectedMetrics, metrics),
//                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
//                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
//                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),
//                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
//                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
//            );
//        }
//    }
//}
