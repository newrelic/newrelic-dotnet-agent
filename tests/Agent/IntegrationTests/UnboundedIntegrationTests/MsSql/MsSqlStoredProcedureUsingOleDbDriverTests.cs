// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlStoredProcedureUsingOleDbDriverTestsBase : NewRelicIntegrationTest<OleDbBasicMvcFixture>
    {
        private readonly OleDbBasicMvcFixture _fixture;
        private readonly bool _paramsWithAtSigns;

        public MsSqlStoredProcedureUsingOleDbDriverTestsBase(OleDbBasicMvcFixture fixture, ITestOutputHelper output, bool paramsWithAtSigns) : base(fixture)
        {
            _paramsWithAtSigns = paramsWithAtSigns;

            _fixture = fixture;
            _fixture.TestLogger = output;
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
                    _fixture.GetMsSqlParameterizedStoredProcedureUsingOleDbDriver(_paramsWithAtSigns);
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
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOleDbDriver"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/Other/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                ? DbParameterData.OleDbMsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.OleDbMsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/Other/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOleDbDriver",
                    Sql = _fixture.ProcedureName,
                    DatastoreMetricName = $"Datastore/statement/Other/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOleDbDriver");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOleDbDriver");
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
            var logEntries = _fixture.AgentLog.GetFileLines().ToList();

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
            );
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOleDbDriverTests : MsSqlStoredProcedureUsingOleDbDriverTestsBase
    {
        public MsSqlStoredProcedureUsingOleDbDriverTests(OleDbBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOleDbDriverTests_ParamsWithoutAtSigns : MsSqlStoredProcedureUsingOleDbDriverTestsBase
    {
        public MsSqlStoredProcedureUsingOleDbDriverTests_ParamsWithoutAtSigns(OleDbBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture, output, false)
        {
        }
    }
}
