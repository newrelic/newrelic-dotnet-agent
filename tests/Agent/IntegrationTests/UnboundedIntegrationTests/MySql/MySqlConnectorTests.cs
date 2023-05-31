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

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlConnectorTestBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _testAction;

        protected MySqlConnectorTestBase(TFixture fixture, ITestOutputHelper output, string testAction) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetLogger(output);

            _fixture.AddCommand($"MySqlConnectorExerciser {testAction}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(15);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(15);

                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = string.Format(@"{0}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", fixture.DestinationNewRelicExtensionsDirectoryPath);
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2)); // must be 2 minutes since this can take a while.
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
            _testAction = testAction;
        }

        [Fact]
        public void Test()
        {
            var transactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlConnectorExerciser/{_testAction}";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MySQL/{CommonUtils.NormalizeHostname(MySqlTestConfiguration.MySqlServer)}/{MySqlTestConfiguration.MySqlPort}", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = 1, metricScope = transactionName },
            };

            // only check "Iterate" metrics for ExecuteReader calls
            if (transactionName.IndexOf("Reader", StringComparison.Ordinal) != -1)
            {
                //This value is dictated by the query that is being run as part of this test. In this case, we're running a query that returns a single row.
                //This results in two calls to Read followed by a call to NextResult. Therefore the call count for the Iterate metric should be 3.
                var expectedIterateCallCount = 3;

                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount });
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterateCallCount, metricScope = transactionName });
            }

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a non-web transaction so there should be no allWeb metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allWeb", callCount = 1 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 1, metricScope = transactionName }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MySQL/dates/select"
            };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = transactionName,
                    Sql = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?",
                    DatastoreMetricName = "Datastore/statement/MySQL/dates/select",

                    HasExplainPlan = true
                }
            };

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "sql", parameterValue = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MySqlTestConfiguration.MySqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "port_path_or_id", parameterValue = MySqlTestConfiguration.MySqlPort},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "database_name", parameterValue = MySqlTestConfiguration.MySqlDbName},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "explain_plan"}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(transactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(transactionName);
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
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample)
            );
        }
    }

    #region ExecuteReader Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteReaderTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteReaderTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteReaderTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteReaderTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteReaderTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteReaderTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteReaderTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteReaderTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReader")
        {
        }
    }

    #endregion

    #region ExecuteScalar Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteScalarTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteScalarTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteScalarTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteScalarTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteScalarTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteScalarTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    [NetCoreTest]
    [NetCoreTest]
    public class MySqlConnectorExecuteScalarTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteScalarTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalar")
        {
        }
    }

    #endregion


    #region ExecuteNonQuery Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteNonQueryTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteNonQueryTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteNonQueryTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteNonQueryTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteNonQueryTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteNonQueryTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQuery")
        {
        }
    }

    #endregion

    #region ExecuteReaderAsync Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteReaderAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteReaderAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteReaderAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteReaderAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteReaderAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteReaderAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteReaderAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteReaderAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteReaderAsync")
        {
        }
    }

    #endregion

    #region ExecuteScalarAsync Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteScalarAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteScalarAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteScalarAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteScalarAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteScalarAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteScalarAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteScalarAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteScalarAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteScalarAsync")
        {
        }
    }

    #endregion

    #region ExecuteNonQueryAync Tests

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorExecuteNonQueryAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorExecuteNonQueryAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "ExecuteNonQueryAsync")
        {
        }
    }

    #endregion

    # region DbCommandExecuteReader Tests 

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteReaderTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteReaderTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteReaderTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteReaderTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteReaderTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteReaderTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteReaderTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteReaderTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReader")
        {
        }
    }

    #endregion

    #region DbCommandExecuteScalar Tests

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteScalarTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteScalarTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteScalarTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteScalarTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteScalarTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteScalarTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteScalarTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteScalarTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalar")
        {
        }
    }

    #endregion

    #region DbCommandExecuteNonQuery Tests

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQuery")
        {
        }
    }

    #endregion

    #region DbCommandExecuteReaderAsync Tests 

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteReaderAsync")
        {
        }
    }

    #endregion

    #region DbCommandExecuteScalarAsync Tests

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteScalarAsync")
        {
        }
    }

    #endregion

    #region DbCommandExecuteNonQueryAsync Tests

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, "DbCommandExecuteNonQueryAsync")
        {
        }
    }

    #endregion
}
