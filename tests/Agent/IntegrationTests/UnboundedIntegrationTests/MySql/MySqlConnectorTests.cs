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
    public abstract class MySqlConnectorTestBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private readonly List<string> commandList = new List<string>()
        {
            "ExecuteReader",
            "ExecuteScalar",
            "ExecuteNonQuery",
            "ExecuteReaderAsync",
            "ExecuteScalarAsync",
            "ExecuteNonQueryAsync",
            "DbCommandExecuteReader",
            "DbCommandExecuteScalar",
            "DbCommandExecuteNonQuery",
            "DbCommandExecuteReaderAsync",
            "DbCommandExecuteScalarAsync",
            "DbCommandExecuteNonQueryAsync"
        };

        protected MySqlConnectorTestBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            foreach (var command in commandList)
                _fixture.AddCommand($"MySqlConnectorExerciser {command}");


            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier
                        .ConfigureFasterMetricsHarvestCycle(15)
                        .ConfigureFasterTransactionTracesHarvestCycle(15)
                        .ConfigureFasterSqlTracesHarvestCycle(15)
                        .ForceTransactionTraces()
                        .SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath,
                        new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath,
                        new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "",
                        "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex,
                        TimeSpan.FromMinutes(2)); // must be 2 minutes since this can take a while.
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
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/all", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allOther", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MySQL/{CommonUtils.NormalizeHostname(MySqlTestConfiguration.MySqlServer)}/{MySqlTestConfiguration.MySqlPort}", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = commandList.Count },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = commandList.Count },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a non-web transaction so there should be no allWeb metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allWeb", callCount = 1 },
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>();

            foreach (var command in commandList)
            {
                var transactionName = GetTransactionName(command);

                expectedMetrics.Add(new Assertions.ExpectedMetric
                {
                    metricName = @"Datastore/statement/MySQL/dates/select",
                    callCount = 1,
                    metricScope = transactionName
                });

                // only check "Iterate" metrics for ExecuteReader calls
                if (transactionName.IndexOf("Reader", StringComparison.Ordinal) != -1)
                {
                    //This value is dictated by the query that is being run as part of this test. In this case, we're running a query that returns a single row.
                    //This results in two calls to Read followed by a call to NextResult.
                    //Therefore the call count for the Iterate metric should be 3. The unscoped Iterate metric should have a count of commandList.Count (3 x 4)

                    expectedMetrics.Add(new Assertions.ExpectedMetric
                    {
                        metricName = @"DotNet/DatabaseResult/Iterate",
                        callCount = commandList.Count
                    });
                    expectedMetrics.Add(new Assertions.ExpectedMetric
                    {
                        metricName = @"DotNet/DatabaseResult/Iterate",
                        callCount = 3,
                        metricScope = transactionName
                    });
                }

                // The operation metric should not be scoped because the statement metric is scoped instead
                unexpectedMetrics.Add(
                    new Assertions.ExpectedMetric
                    {
                        metricName = @"Datastore/operation/MySQL/select",
                        callCount = 1,
                        metricScope = transactionName
                    });
            }

            // only a single sql trace is expected - it will be the command that was slowest, which is always the first command
            expectedSqlTraces.Add(new
                Assertions.ExpectedSqlTrace
            {
                TransactionName = GetTransactionName(commandList.First()),
                Sql = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?",
                DatastoreMetricName = "Datastore/statement/MySQL/dates/select",
                HasExplainPlan = true
            });


            var expectedTransactionTraceSegments = new List<string> { "Datastore/statement/MySQL/dates/select" };

            var expectedTransactionEventIntrinsicAttributes = new List<string> { "databaseDuration" };

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter
                {
                    segmentName = "Datastore/statement/MySQL/dates/select",
                    parameterName = "sql",
                    parameterValue = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?"
                },
                new Assertions.ExpectedSegmentParameter
                {
                    segmentName = "Datastore/statement/MySQL/dates/select",
                    parameterName = "host",
                    parameterValue = CommonUtils.NormalizeHostname(MySqlTestConfiguration.MySqlServer)
                },
                new Assertions.ExpectedSegmentParameter
                {
                    segmentName = "Datastore/statement/MySQL/dates/select",
                    parameterName = "port_path_or_id",
                    parameterValue = MySqlTestConfiguration.MySqlPort
                },
                new Assertions.ExpectedSegmentParameter
                {
                    segmentName = "Datastore/statement/MySQL/dates/select",
                    parameterName = "database_name",
                    parameterValue = MySqlTestConfiguration.MySqlDbName
                },
                new Assertions.ExpectedSegmentParameter
                {
                    segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "explain_plan"
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();


            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );

            // only a single transaction trace is expected - it will be the command that was slowest, which is always the first command
            var sampledTransactionName = GetTransactionName(commandList.First());
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(sampledTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(sampledTransactionName);

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );
            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample)
            );
        }

        private static string GetTransactionName(string command) => $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlConnectorExerciser/{command}";
    }

    [NetFrameworkTest]
    public class MySqlConnectorTestFW462 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlConnectorTestFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorTestFW471 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlConnectorTestFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorTestFW48 : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlConnectorTestFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorTestFWLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlConnectorTestFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorTestCoreOldest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlConnectorTestCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MySqlConnectorTestCoreLatest : MySqlConnectorTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlConnectorTestCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
