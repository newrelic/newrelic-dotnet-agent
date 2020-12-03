// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


//using System;
//using System.Collections.Generic;
//using System.Linq;
//using NewRelic.Agent.IntegrationTestHelpers;
//using NewRelic.Agent.IntegrationTests.Shared;
//using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
//using NewRelic.Testing.Assertions;
//using Xunit;
//using Xunit.Abstractions;

//namespace NewRelic.Agent.UnboundedIntegrationTests.IbmDb2
//{
//    [NetFrameworkTest]
//    public class IbmDb2StoredProcedureTests : NewRelicIntegrationTest<IbmDb2BasicMvcFixture>
//    {
//        private readonly IbmDb2BasicMvcFixture _fixture;
//        private readonly string _procedureName = $"IbmDb2TestStoredProc{Guid.NewGuid():N}";

//        public IbmDb2StoredProcedureTests(IbmDb2BasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
//                    configModifier.SetLogLevel("finest");

//                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
//                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
//                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
//                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
//                },
//                exerciseApplication: () =>
//                {
//                    _fixture.IbmDb2ParameterizedStoredProcedure(_procedureName);
//                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex);
//                }
//            );
//            _fixture.Initialize();
//        }

//        [Fact]
//        public void Test()
//        {
//            var expectedMetrics = new List<Assertions.ExpectedMetric>
//            {
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
//                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/IBMDB2/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = "WebTransaction/MVC/IbmDb2Controller/IbmDb2ParameterizedStoredProcedure"}
//            };

//            var expectedTransactionTraceSegments = new List<string>
//            {
//                $"Datastore/statement/IBMDB2/{_procedureName.ToLower()}/ExecuteProcedure"
//            };

//            var expectedQueryParameters = DbParameterData.IbmDb2Parameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue);

//            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/IBMDB2/{_procedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

//            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
//            {
//                new Assertions.ExpectedSqlTrace
//                {
//                    TransactionName = "WebTransaction/MVC/IbmDb2Controller/IbmDb2ParameterizedStoredProcedure",
//                    Sql = _procedureName,
//                    DatastoreMetricName = $"Datastore/statement/IBMDB2/{_procedureName.ToLower()}/ExecuteProcedure",
//                    QueryParameters = expectedQueryParameters,
//                    HasExplainPlan = false
//                }
//            };

//            var metrics = _fixture.AgentLog.GetMetrics().ToList();
//            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/IbmDb2Controller/IbmDb2ParameterizedStoredProcedure");
//            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/IbmDb2Controller/IbmDb2ParameterizedStoredProcedure");
//            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

//            NrAssert.Multiple(
//                () => Assert.NotNull(transactionSample),
//                () => Assert.NotNull(transactionEvent)
//            );

//            NrAssert.Multiple
//            (
//                () => Assertions.MetricsExist(expectedMetrics, metrics),
//                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
//                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),
//                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
//            );
//        }
//    }
//}
