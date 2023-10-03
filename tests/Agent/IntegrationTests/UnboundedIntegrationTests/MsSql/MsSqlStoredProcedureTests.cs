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
    public abstract class MsSqlStoredProcedureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly string _tableName;
        private readonly string _procedureName;
        private readonly bool _paramsWithAtSigns;

        public MsSqlStoredProcedureTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, bool paramsWithAtSign) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMsSql();

            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlParameterizedStoredProcedure";
            _paramsWithAtSigns = paramsWithAtSign;

            _tableName = Utilities.GenerateTableName();
            _procedureName = Utilities.GenerateProcedureName();

            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} MsSqlParameterizedStoredProcedure {_procedureName} {paramsWithAtSign}");
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
                    configModifier.SetLogLevel("finest");       //This has to stay at finest to ensure parameter check security

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
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = _expectedTransactionName }
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MSSQL/{_procedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                    ? DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                    : DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);


            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/{_procedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = _procedureName,
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_procedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters,
                    HasExplainPlan = true
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

    #region System.Data (.NET Framework only)
    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_SystemData_FWLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_SystemData_NoAtSigns_FWLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureTests_SystemData_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: false)
        {
        }
    }
    #endregion

    #region System.Data.SqlClient (.NET Core/5+ only)

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_SystemDataSqlClient_CoreLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlStoredProcedureTests_SystemDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_SystemDataSqlClient_NoAtSigns_CoreLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlStoredProcedureTests_SystemDataSqlClient_NoAtSigns_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_SystemDataSqlClient_CoreOldest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlStoredProcedureTests_SystemDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_SystemDataSqlClient_NoAtSigns_CoreOldest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlStoredProcedureTests_SystemDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    #endregion


    #region Microsoft.Data.SqlClient (FW and Core/5+)

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_FWLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_FW462 : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_FW462 : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_CoreLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_CoreOldest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest : MsSqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlStoredProcedureTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }
    #endregion
}
