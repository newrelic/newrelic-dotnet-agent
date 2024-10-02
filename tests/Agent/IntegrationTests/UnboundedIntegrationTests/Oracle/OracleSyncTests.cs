// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Oracle
{
    public abstract class OracleSyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _tableName;

        protected OracleSyncTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _tableName = GenerateTableName();

            _fixture.AddCommand($"OracleExerciser InitializeTable {_tableName}"); // creates a new table. The table gets dropped automatically when the exerciser goes out of scope
            _fixture.AddCommand($"OracleExerciser ExerciseSync");

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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedDatastoreCallCount = 4; // SELECT, INSERT, COUNT, and DELETE from GetOracle() above

            //This value is dictated by the query that is being run as part of this test. In this case, we're running a query that returns a single row.
            //This results in two calls to read. Therefore the call count for the Iterate metric should be 2.
            var expectedIterateCallCount = 2;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new() { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
                new() { metricName = @"Datastore/Oracle/all", callCount = expectedDatastoreCallCount },
                new() { metricName = @"Datastore/Oracle/allOther", callCount = expectedDatastoreCallCount },
                new() { metricName = $@"Datastore/instance/Oracle/{OracleConfiguration.OracleServer}/{OracleConfiguration.OraclePort}", callCount = expectedDatastoreCallCount},
                new() { metricName = @"Datastore/operation/Oracle/select", callCount = 2 },
                new() { metricName = @"Datastore/statement/Oracle/user_tables/select", callCount = 1 },
                new() { metricName = @"Datastore/statement/Oracle/user_tables/select", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync"},
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/select", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/select", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync"},
                new() { metricName = @"Datastore/operation/Oracle/insert", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/insert", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/insert", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync"},
                new() { metricName = @"Datastore/operation/Oracle/delete", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/delete", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_tableName}/delete", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync"},

                new() { metricName = @"DotNet/DatabaseResult/Iterate" , callCount = expectedIterateCallCount },
                new() { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync"}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a non-web transaction so there should be no allWeb metrics
                new() { metricName = @"Datastore/allWeb" },
                new() { metricName = @"Datastore/Oracle/allWeb"},

                // The operation metric should not be scoped because the statement metric is scoped instead
                new() { metricName = @"Datastore/operation/Oracle/select", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync" },
                new() { metricName = @"Datastore/operation/Oracle/insert", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync" },
                new() { metricName = @"Datastore/operation/Oracle/delete", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync" }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/Oracle/user_tables/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new()
                {
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync",
                    Sql = "SELECT DEGREE FROM user_tables WHERE ROWNUM <= ?",
                    DatastoreMetricName = "Datastore/statement/Oracle/user_tables/select",
                    HasExplainPlan = false
                },
                new()
                {
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync",
                    Sql = $"SELECT COUNT(*) FROM {_tableName}",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_tableName}/select",

                    HasExplainPlan = false
                },
                new()
                {
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync",
                    Sql = $"INSERT INTO {_tableName} (HOTEL_ID, BOOKING_DATE) VALUES (?, SYSDATE)",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_tableName}/insert",

                    HasExplainPlan = false
                },
                new()
                {
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync",
                    Sql = $"DELETE FROM {_tableName} WHERE HOTEL_ID = ?",
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_tableName}/delete",

                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseSync");
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            Assert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
                );

            Assert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }

        private static string GenerateTableName()
        {
            //Oracle tables must start w/ character and be <= 30 length. Table name = H{tableId}
            var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
            return $"h{tableId}";
        }
    }

    [NetFrameworkTest]
    public class OracleSyncTestsFramework462 : OracleSyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public OracleSyncTestsFramework462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class OracleSyncTestsFramework471 : OracleSyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public OracleSyncTestsFramework471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class OracleSyncTestsFrameworkLatest : OracleSyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public OracleSyncTestsFrameworkLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class OracleSyncTestsCoreLatest : OracleSyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public OracleSyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
