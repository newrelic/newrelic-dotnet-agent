// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;

        private readonly string _tableName;
        private readonly string _libraryName;

        public MsSqlAsyncTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, string libraryName) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlAsync";
            _tableName = Utilities.GenerateTableName();
            _libraryName = libraryName;

            _fixture.AddCommand($"{excerciserName} CreateTable {_tableName}");
            _fixture.AddCommand($"{excerciserName} MsSqlAsync {_tableName}");
            _fixture.AddCommand($"{excerciserName} Wait 5000");
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

            //This value is dictated by the query and subsequent ExecuteScalarAsync that is being run as part of this test. In this case, we're running a query that returns a single row.
            //This results in a call to ReadAsync followed by a NextResultAsync and finally another ReadAsync. Therefore
            //the call count for the Iterate metric should be 3.
            var expectedIterateCallCount = 3;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },

                new Assertions.ExpectedMetric { metricName = $"DotNet/{_libraryName}.SqlConnection/OpenAsync", callCount = 1 },
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
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/delete", callCount = 1, metricScope = _expectedTransactionName },

                // Don't double count the open
                new Assertions.ExpectedMetric { metricName = $"DotNet/{_libraryName}.SqlConnection/Open" },
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MSSQL/teammembers/select"
            };
            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?"},
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
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = ?",
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
                    TransactionName = _expectedTransactionName,
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

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

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
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }
    }

    [NetFrameworkTest]
    public class MsSqlAsyncTests_SystemData_FWLatest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlAsyncTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  libraryName: "System.Data.SqlClient")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlAsyncTests_SystemDataSqlClient_CoreLatest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlAsyncTests_SystemDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  libraryName: "System.Data.SqlClient")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlAsyncTests_SystemDataSqlClient_CoreOldest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlAsyncTests_SystemDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  libraryName: "System.Data.SqlClient")
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlAsyncTests_MicrosoftDataSqlClient_FWLatest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlAsyncTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlAsyncTests_MicrosoftDataSqlClient_FW462 : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlAsyncTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }


    [NetCoreTest]
    public class MsSqlAsyncTests_MicrosoftDataSqlClient_CoreLatest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlAsyncTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }

    [NetCoreTest]
    public class MsSqlAsyncTests_MicrosoftDataSqlClient_CoreOldest : MsSqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlAsyncTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  libraryName: "Microsoft.Data.SqlClient")
        {
        }
    }
}
