// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlQueryParamAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly string _tableName;
        private readonly bool _paramsWithAtSign;

        public MsSqlQueryParamAsyncTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, bool paramsWithAtSign) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMsSql();

            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlAsync_WithParameterizedQuery";
            _tableName = Utilities.GenerateTableName();

            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} MsSqlAsync_WithParameterizedQuery {_tableName} {paramsWithAtSign}");
            _fixture.AddCommand($"{excerciserName} Wait 5000");
            _fixture.AddCommand($"{excerciserName} DropTable {_tableName}");

            _paramsWithAtSign = paramsWithAtSign;

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
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
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
            var expectedDatastoreCallCount = 4;
            //This value is dictated by the query and subsequent ExecuteScalarAsync that is being run as part of this test. In this case, we're running a query that returns
            //a single row.
            //This results in a call to ReadAsync followed by a NextResultAsync and finally a another ReadAsync. Therefore the call count for the Iterate metric should be 4.
            var expectedIterateCallCount = 3;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther", callCount = expectedDatastoreCallCount },

                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/insert", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/delete", callCount = 1, metricScope = _expectedTransactionName},

                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount, metricScope = _expectedTransactionName}
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 3, metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 1, metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1, metricScope = _expectedTransactionName }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MSSQL/teammembers/select"
            };
            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"}
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"SELECT COUNT(*) FROM {_tableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/select",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName =_expectedTransactionName,
                    Sql = $"INSERT INTO {_tableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/insert",

                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = $"DELETE FROM {_tableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/delete",

                    HasExplainPlan = true
                }
            };

            var expectedQueryParameters = _paramsWithAtSign
                ? DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.MsSqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

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
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
            );
        }
    }


    #region System.Data (.NET Framework only)
    [NetFrameworkTest]
    public class MsSqlQueryParamAsyncTests_SystemData_FWLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamAsyncTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamAsyncTests_SystemData_NoAtSigns_FWLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamAsyncTests_SystemData_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
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
    public class MsSqlQueryParamAsyncTests_SystemDataSqlClient_CoreLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamAsyncTests_SystemDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_SystemDataSqlClient_NoAtSigns_CoreLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamAsyncTests_SystemDataSqlClient_NoAtSigns_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_SystemDataSqlClient_CoreOldest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamAsyncTests_SystemDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_SystemDataSqlClient_NoAtSigns_CoreOldest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamAsyncTests_SystemDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
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
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_FWLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_FW462 : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_FW462 : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_CoreLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_CoreOldest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest : MsSqlQueryParamAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamAsyncTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
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
