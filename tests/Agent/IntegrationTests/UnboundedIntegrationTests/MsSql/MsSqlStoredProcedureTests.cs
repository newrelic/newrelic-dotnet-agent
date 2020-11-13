// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlStoredProcedureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture:RemoteApplicationFixture, IMsSqlClientFixture
    {
        private readonly IMsSqlClientFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _paramsWithAtSigns;

        public MsSqlStoredProcedureTestsBase(TFixture fixture, ITestOutputHelper output, string expectedTransactionName, bool paramsWithAtSigns) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = expectedTransactionName;
            _paramsWithAtSigns = paramsWithAtSigns;

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");       //This has to stay at finest to ensure parameter check security

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMsSqlParameterizedStoredProcedure(_paramsWithAtSigns);
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
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = _expectedTransactionName }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MSSQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                    ? DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                    : DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);


            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = _fixture.ProcedureName,
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_fixture.ProcedureName.ToLower()}/ExecuteProcedure",
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

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests : MsSqlStoredProcedureTestsBase<MsSqlBasicMvcFixture>
    {
        public MsSqlStoredProcedureTests(MsSqlBasicMvcFixture fixture, ITestOutputHelper output) 
            : base(fixture, output, "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedure", true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_ParamsWithoutAtSigns : MsSqlStoredProcedureTestsBase<MsSqlBasicMvcFixture>
    {
        public MsSqlStoredProcedureTests_ParamsWithoutAtSigns(MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MsSqlController/MsSqlParameterizedStoredProcedure", false)
        {
        }
    }


    [NetCoreTest]
    public class MicrosoftDataSqlClientStoredProcedureTests : MsSqlStoredProcedureTestsBase<MicrosoftDataSqlClientFixture>
    {
        public MicrosoftDataSqlClientStoredProcedureTests(MicrosoftDataSqlClientFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MicrosoftDataSqlClient/MsSqlParameterizedStoredProcedure/{procedureName}/{paramsWithAtSign}", true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftDataSqlClientStoredProcedureTests_ParamsWithoutAtSigns : MsSqlStoredProcedureTestsBase<MicrosoftDataSqlClientFixture>
    {
        public MicrosoftDataSqlClientStoredProcedureTests_ParamsWithoutAtSigns(MicrosoftDataSqlClientFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MicrosoftDataSqlClient/MsSqlParameterizedStoredProcedure/{procedureName}/{paramsWithAtSign}", false)
        {
        }
    }
}
