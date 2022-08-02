// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlStoredProcedureTestsBase : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFWLatest>
    {
        private readonly ConsoleDynamicMethodFixtureFWLatest _fixture;
        private readonly bool _paramsWithAtSigns;

        // TODO: this follows the pattern in the existing CosmosDB tests but does it make sense here?
        private string procedureName = "testProcedure" + Guid.NewGuid().ToString("n").Substring(0, 4);

        protected MySqlStoredProcedureTestsBase(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, bool paramsWithAtSigns) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _paramsWithAtSigns = paramsWithAtSigns;

            _fixture.AddCommand($"MySqlExerciser ExecuteStoredProcedure {procedureName} {paramsWithAtSigns}");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlExerciser/ExecuteStoredProcedure";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                //new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                //new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = transactionName}
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{procedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{procedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = transactionName}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                //$"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure"
                $"Datastore/statement/MySQL/{procedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                ? DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters
            {
                //segmentName = $"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
                segmentName = $"Datastore/statement/MySQL/{procedureName.ToLower()}/ExecuteProcedure",
                QueryParameters = expectedQueryParameters
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = transactionName,
                    //Sql = _fixture.ProcedureName,
                    Sql = procedureName,
                    //DatastoreMetricName = $"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
                    DatastoreMetricName = $"Datastore/statement/MySQL/{procedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters,
                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(transactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(transactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );

        }
    }


    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithAtSigns : MySqlStoredProcedureTestsBase
    {
        public MySqlStoredProcedureTestsWithAtSigns(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithoutAtSigns : MySqlStoredProcedureTestsBase
    {
        public MySqlStoredProcedureTestsWithoutAtSigns(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }


}
