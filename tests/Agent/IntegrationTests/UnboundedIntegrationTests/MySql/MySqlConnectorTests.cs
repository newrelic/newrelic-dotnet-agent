// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlConnectorTestBase : IClassFixture<RemoteServiceFixtures.MySqlConnectorBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.MySqlConnectorBasicMvcFixture _fixture;
        private readonly string _transactionName;

        protected MySqlConnectorTestBase(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output, Action<RemoteServiceFixtures.MySqlConnectorBasicMvcFixture> exerciseApplication, string transactionName)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = string.Format(@"{0}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", fixture.DestinationNewRelicExtensionsDirectoryPath);
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () => exerciseApplication(_fixture)
            );
            _fixture.Initialize();
            _transactionName = transactionName;
        }

        [Fact]
        public void Test()
        {
            var transactionName = $"WebTransaction/MVC/MySqlConnectorController/{_transactionName}";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allWeb", callCount = 1 },
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
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allOther", callCount = 1 },

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

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteReaderTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteReader(), "ExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteScalarTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteScalar(), "ExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteNonQueryTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteNonQuery(), "ExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteReaderAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteReaderAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteReaderAsync(), "ExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteScalarAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteScalarAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteScalarAsync(), "ExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorExecuteNonQueryAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorExecuteNonQueryAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetExecuteNonQueryAsync(), "ExecuteNonQueryAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteReaderTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteReader(), "DbCommandExecuteReader")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteScalarTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteScalar(), "DbCommandExecuteScalar")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteNonQueryTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteNonQuery(), "DbCommandExecuteNonQuery")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteReaderAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteReaderAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteReaderAsync(), "DbCommandExecuteReaderAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteScalarAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteScalarAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteScalarAsync(), "DbCommandExecuteScalarAsync")
        {
        }
    }

    [NetFrameworkTest]
    public class MySqlConnectorDbCommandExecuteNonQueryAsyncTest : MySqlConnectorTestBase
    {
        public MySqlConnectorDbCommandExecuteNonQueryAsyncTest(RemoteServiceFixtures.MySqlConnectorBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, x => x.GetDbCommandExecuteNonQueryAsync(), "DbCommandExecuteNonQueryAsync")
        {
        }
    }

}
