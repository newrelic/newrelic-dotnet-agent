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
    public abstract class MySqlAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private bool _asyncOpen;

        public MySqlAsyncTestsBase(TFixture fixture, ITestOutputHelper output, bool asyncOpen) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"MySqlExerciser SingleDateQueryAsync");

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

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
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
            // AsyncOpen() is only supported in certain versions of the library
            _asyncOpen = asyncOpen;
        }

        [Fact]
        public void Test()
        {
            var transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlExerciser/SingleDateQueryAsync";
            Assertions.ExpectedMetric openMetric;

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

            if (_asyncOpen)
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/MySql.Data.MySqlClient.MySqlConnection/OpenAsync", callCount = 1 });
            }
            else
            {
                expectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/MySql.Data.MySqlClient.MySqlConnection/Open", callCount = 1 });
            }

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a console app so there should be no allWeb metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allWeb", callCount = 1 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 1, metricScope = transactionName }
            };

            // Don't double count the Open
            if (_asyncOpen)
            {
                unexpectedMetrics.Add(new Assertions.ExpectedMetric { metricName = @"DotNet/MySql.Data.MySqlClient.MySqlConnection/Open", callCount = 1 });
            }

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
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "explain_plan"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "database_name", parameterValue = MySqlTestConfiguration.MySqlDbName}

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
    public class MySqlAsyncTestsFW462 : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlAsyncTestsFW471 : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlAsyncTestsFW48 : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlAsyncTestsFWLatest : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetCoreTest]
    public class MySqlAsyncTestsCoreOldest : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlAsyncTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetCoreTest]
    public class MySqlAsyncTestsCore : MySqlAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlAsyncTestsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }
}
