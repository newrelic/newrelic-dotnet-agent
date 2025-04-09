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


namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{

    public abstract class MsSqlStoredProcedureUsingOdbcDriverTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly string _tableName;
        private readonly string _procNameWith;
        private readonly string _procNameWithout;

        public MsSqlStoredProcedureUsingOdbcDriverTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/OdbcParameterizedStoredProcedure";

            _tableName = Utilities.GenerateTableName();
            var procedureName = Utilities.GenerateProcedureName();
            _procNameWith = $"{procedureName}_with";
            _procNameWithout = $"{procedureName}_without";


            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} OdbcParameterizedStoredProcedure {_procNameWith} {_procNameWithout}");
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
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var parameterPlaceholder = string.Join(",", DbParameterData.OdbcMsSqlParameters.Select(_ => "?"));
            var expectedSqlStatementWith = $"{{call {_procNameWith.ToLower()}({parameterPlaceholder})}}";
            var expectedSqlStatementWithout = $"{{call {_procNameWithout.ToLower()}({parameterPlaceholder})}}";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{expectedSqlStatementWith}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{expectedSqlStatementWith}/ExecuteProcedure", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{expectedSqlStatementWithout}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{expectedSqlStatementWithout}/ExecuteProcedure", callCount = 1, metricScope = _expectedTransactionName}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MSSQL/{expectedSqlStatementWith}/ExecuteProcedure",
                $"Datastore/statement/MSSQL/{expectedSqlStatementWithout}/ExecuteProcedure"
            };

            var expectedQueryParametersWith = DbParameterData.OdbcMsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue);
            var expectedQueryParametersWithout = DbParameterData.OdbcMsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParametersWith = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/{expectedSqlStatementWith}/ExecuteProcedure", QueryParameters = expectedQueryParametersWith };
            var expectedTransactionTraceQueryParametersWithout = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/{expectedSqlStatementWithout}/ExecuteProcedure", QueryParameters = expectedQueryParametersWithout };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"{{call {_procNameWith}({parameterPlaceholder})}}",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{expectedSqlStatementWith}/ExecuteProcedure",
                    QueryParameters = expectedQueryParametersWith
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"{{call {_procNameWithout}({parameterPlaceholder})}}",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{expectedSqlStatementWithout}/ExecuteProcedure",
                    QueryParameters = expectedQueryParametersWithout
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
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParametersWith, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParametersWithout, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
            );
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureUsingOdbcDriverTests_FWLatest : MsSqlStoredProcedureUsingOdbcDriverTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureUsingOdbcDriverTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureUsingOdbcDriverTests_CoreLatest : MsSqlStoredProcedureUsingOdbcDriverTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlStoredProcedureUsingOdbcDriverTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser")
        {
        }
    }
}
