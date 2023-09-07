// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlStoredProcedureUsingOleDbDriverTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _paramsWithAtSigns;
        private readonly string _tableName;
        private readonly string _procedureName;

        public MsSqlStoredProcedureUsingOleDbDriverTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, bool paramsWithAtSign) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMsSql();

            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlParameterizedStoredProcedureUsingOleDbDriver";
            _paramsWithAtSigns = paramsWithAtSign;

            _tableName = Utilities.GenerateTableName();
            _procedureName = Utilities.GenerateProcedureName();

            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} MsSqlParameterizedStoredProcedureUsingOleDbDriver {_procedureName} {paramsWithAtSign}");
            _fixture.AddCommand($"{excerciserName} DropTable {_tableName}");

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
                    configModifier.SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/Other/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = _expectedTransactionName}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/Other/{_procedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                ? DbParameterData.OleDbMsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.OleDbMsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/Other/{_procedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = _procedureName,
                    DatastoreMetricName = $"Datastore/statement/Other/{_procedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
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

    // Only tests for System.Data in .NET Framework for OleDb, since the OleDbCommandWrapper is .NET Framework only,
    // and the instrumentation.xml only matches System.Data as of 2022-10-20
    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOleDbDriverTests : MsSqlStoredProcedureUsingOleDbDriverTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureUsingOleDbDriverTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOleDbDriverTests_ParamsWithoutAtSigns : MsSqlStoredProcedureUsingOleDbDriverTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureUsingOleDbDriverTests_ParamsWithoutAtSigns(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: false)
        {
        }
    }
}
