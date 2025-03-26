// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly string _expectedAsyncTransactionName;
        private readonly string _tableName;
        private readonly string _asyncTableName;
        private readonly string _libraryName;

        private readonly bool _isOdbc;

        public MsSqlTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, string libraryName) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMsSql();

            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSql";
            _expectedAsyncTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlAsync";

            //Using different table names for the sync and async calls prevents sql traces from being merged
            _tableName = Utilities.GenerateTableName();
            _asyncTableName = Utilities.GenerateTableName();
            _libraryName = libraryName;

            //Some metrics (connection open and iteration) aren't available in the ODBC instrumentation
            _isOdbc = excerciserName.Contains("Odbc");

            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} CreateTable {_asyncTableName}");
            _fixture.AddCommand($"{excerciserName} MsSql {_tableName}");
            _fixture.AddCommand($"{excerciserName} MsSqlAsync {_asyncTableName}");
            _fixture.AddCommand($"{excerciserName} Wait 5000"); // TBD if this is really necessary

            _fixture.AddCommand($"{excerciserName} DropTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} DropTable {_asyncTableName}");

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
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
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
            var expectedDatastoreCallCount = 8;
            //This value is dictated by the queries that are being run as part of this test. In this case, we're running two queries that each return a single row.
            //This results in a call to Read, NextResult and then a final Read. Therefore the overall call count for the Iterate metric should be 6.
            var expectedIterateCallCount = 6;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther", callCount = expectedDatastoreCallCount },

                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedAsyncTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/select", callCount = 1, metricScope = _expectedAsyncTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/insert", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/insert", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/insert", callCount = 1, metricScope = _expectedAsyncTransactionName},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_tableName}/delete", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/delete", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/{_asyncTableName}/delete", callCount = 1, metricScope = _expectedAsyncTransactionName},
            };

            // The ODBC instrumentation does not instrument connection calls or iterations
            if (! _isOdbc)
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{_libraryName}.SqlConnection/Open", callCount = 1 });
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = $"DotNet/{_libraryName}.SqlConnection/OpenAsync", callCount = 1 });
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount });
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount / 2, metricScope = _expectedTransactionName });
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount / 2, metricScope = _expectedAsyncTransactionName });
            }

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = _expectedAsyncTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/insert", metricScope = _expectedAsyncTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", metricScope = _expectedAsyncTransactionName }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MSSQL/teammembers/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var syncTransactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var syncTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
            var asyncTransactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedAsyncTransactionName);
            var asyncTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedAsyncTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            // Some variable expectations based on which transaction was sampled
            var sampledTransactionSample = syncTransactionSample != null ? syncTransactionSample : asyncTransactionSample;
            var firstOrLastNameQueried = syncTransactionSample != null ? "FirstName" : "LastName";

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = $"SELECT * FROM NewRelic.dbo.TeamMembers WHERE {firstOrLastNameQueried} = ?"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"}

            };

            // The ODBC instrumentation doesn't do explain plans
            var sqlTracesShouldHaveExplainPlans = false;
            if (!_isOdbc)
            {
                expectedTransactionTraceSegmentParameters.Add(new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan" });
                sqlTracesShouldHaveExplainPlans = true;
            }


            // There should be a total of 8 traces: 4 for the sync methods and 4 for the async methods.
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName, //sync select
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedAsyncTransactionName, //async select - note querying LastName
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE LastName = ?",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName, //sync count
                    Sql = $"SELECT COUNT(*) FROM {_tableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/select",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedAsyncTransactionName, //async count
                    Sql = $"SELECT COUNT(*) FROM {_asyncTableName} WITH(nolock)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_asyncTableName}/select",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName, //sync insert
                    Sql = $"INSERT INTO {_tableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/insert",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedAsyncTransactionName, //async insert
                    Sql = $"INSERT INTO {_asyncTableName} (FirstName, LastName, Email) VALUES(?, ?, ?)",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_asyncTableName}/insert",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName, //sync delete
                    Sql = $"DELETE FROM {_tableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_tableName}/delete",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans,
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedAsyncTransactionName, //async delete
                    Sql = $"DELETE FROM {_asyncTableName} WHERE Email = ?",
                    DatastoreMetricName = $"Datastore/statement/MSSQL/{_asyncTableName}/delete",

                    HasExplainPlan = sqlTracesShouldHaveExplainPlans
                }
            };

            NrAssert.Multiple(
                () => Assert.NotNull(sampledTransactionSample),
                () => Assert.NotNull(syncTransactionEvent),
                () => Assert.NotNull(asyncTransactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, sampledTransactionSample),

                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, sampledTransactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, syncTransactionEvent),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, asyncTransactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }


    #region System.Data.SqlClient
    [NetFrameworkTest]
    public class MsSqlTests_SystemData_FWLatest : MsSqlTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  libraryName: "System.Data.SqlClient")
        {
        }
    }
    #endregion

    #region Microsoft.Data.SqlClient

    [NetFrameworkTest]
    public class MsSqlTests_MicrosoftDataSqlClient_FWLatest : MsSqlTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlTests_MicrosoftDataSqlClient_FW462 : MsSqlTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlTests_MicrosoftDataSqlClient_CoreOldest : MsSqlTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlTests_MicrosoftDataSqlClient_CoreLatest : MsSqlTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    #endregion

    #region System.Data.Odbc

    [NetFrameworkTest]
    public class MsSqlTests_SystemDataOdbc_FWLatest : MsSqlTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlTests_SystemDataOdbc_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser",
                  libraryName: "System.Data.Odbc")
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlTests_SystemDataOdbc_FW462 : MsSqlTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlTests_SystemDataOdbc_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser",
                  libraryName: "System.Data.Odbc")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlTests_SystemDataOdbc_CoreLatest : MsSqlTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlTests_SystemDataOdbc_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser",
                  libraryName: "System.Data.Odbc")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlTests_SystemDataOdbc_CoreOldest : MsSqlTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlTests_SystemDataOdbc_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataOdbcExerciser",
                  libraryName: "System.Data.Odbc")
        {
        }
    }

    #endregion

}
