// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlStoredProcedureTestsBase : IClassFixture<MySqlBasicMvcFixture>
    {
        private readonly MySqlBasicMvcFixture _fixture;
        private readonly bool _paramsWithAtSigns;

        protected MySqlStoredProcedureTestsBase(MySqlBasicMvcFixture fixture, ITestOutputHelper output, bool paramsWithAtSigns)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _paramsWithAtSigns = paramsWithAtSigns;

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
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMySqlParameterizedStoredProcedure(paramsWithAtSigns);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {

            var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = "WebTransaction/MVC/MySqlController/MySqlParameterizedStoredProcedure"}
        };

            var expectedTransactionTraceSegments = new List<string>
        {
            $"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure"
        };

            var expectedQueryParameters = _paramsWithAtSigns
                ? DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters
            {
                segmentName = $"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
                QueryParameters = expectedQueryParameters
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/MySqlController/MySqlParameterizedStoredProcedure",
                    Sql = _fixture.ProcedureName,
                    DatastoreMetricName = $"Datastore/statement/MySQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters,
                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/MySqlController/MySqlParameterizedStoredProcedure");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/MySqlController/MySqlParameterizedStoredProcedure");
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
        public MySqlStoredProcedureTestsWithAtSigns(MySqlBasicMvcFixture fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithoutAtSigns : MySqlStoredProcedureTestsBase
    {
        public MySqlStoredProcedureTestsWithoutAtSigns(MySqlBasicMvcFixture fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }


}
