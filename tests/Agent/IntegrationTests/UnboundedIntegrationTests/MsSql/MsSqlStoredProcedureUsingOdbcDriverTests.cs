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

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{

    public abstract class MsSqlStoredProcedureUsingOdbcDriverTestsBase : NewRelicIntegrationTest<OdbcBasicMvcFixture>
    {
        private readonly OdbcBasicMvcFixture _fixture;
        private readonly bool _paramsWithAtSigns;
        public MsSqlStoredProcedureUsingOdbcDriverTestsBase(OdbcBasicMvcFixture fixture, ITestOutputHelper output, bool paramsWithAtSigns) : base(fixture)
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
                    _fixture.GetMsSqlParameterizedStoredProcedureUsingOdbcDriver(_paramsWithAtSigns);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var parameterPlaceholder = string.Join(",", DbParameterData.OdbcMsSqlParameters.Select(_ => "?"));
            var expectedSqlStatement = $"{{call {_fixture.ProcedureName.ToLower()}({parameterPlaceholder})}}";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{expectedSqlStatement}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{expectedSqlStatement}/ExecuteProcedure", callCount = 1, metricScope = "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOdbcDriver"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/Other/{expectedSqlStatement}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                    ? DbParameterData.OdbcMsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                    : DbParameterData.OdbcMsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/Other/{expectedSqlStatement}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOdbcDriver",
                    Sql = $"{{call {_fixture.ProcedureName}({parameterPlaceholder})}}",
                    DatastoreMetricName = $"Datastore/statement/Other/{expectedSqlStatement}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOdbcDriver");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedureUsingOdbcDriver");
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
    public class MsSqlStoredProcedureUsingOdbcDriverTests : MsSqlStoredProcedureUsingOdbcDriverTestsBase
    {
        public MsSqlStoredProcedureUsingOdbcDriverTests(OdbcBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture, output, true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOdbcDriverTests_ParamsWithoutAtSigns : MsSqlStoredProcedureUsingOdbcDriverTestsBase
    {
        public MsSqlStoredProcedureUsingOdbcDriverTests_ParamsWithoutAtSigns(OdbcBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture, output, false)
        {
        }
    }
}
