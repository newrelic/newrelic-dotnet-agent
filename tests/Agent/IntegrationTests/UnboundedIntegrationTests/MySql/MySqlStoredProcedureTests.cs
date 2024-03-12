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

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlStoredProcedureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _procNameWith;
        private readonly string _procNameWithout;

        protected MySqlStoredProcedureTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMySql();

            _fixture = fixture;
            _fixture.TestLogger = output;

            string procedureName = "testProcedure" + Guid.NewGuid().ToString("n").Substring(0, 4);
            _procNameWith = $"{procedureName}_with";
            _procNameWithout = $"{procedureName}_without";
            _fixture.AddCommand($"MySqlExerciser CreateAndExecuteStoredProcedures {_procNameWith} {_procNameWithout}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(10);
                    configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(10);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2)); // must be 2 minutes since this can take a while.
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlExerciser/CreateAndExecuteStoredProcedures";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procNameWith.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procNameWith.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = transactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procNameWithout.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procNameWithout.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = transactionName}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MySQL/{_procNameWith.ToLower()}/ExecuteProcedure",
                $"Datastore/statement/MySQL/{_procNameWithout.ToLower()}/ExecuteProcedure"

            };

            var expectedQueryParametersWith = DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue);
            var expectedQueryParametersWithout = DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParametersWith = new Assertions.ExpectedSegmentQueryParameters
            {
                segmentName = $"Datastore/statement/MySQL/{_procNameWith.ToLower()}/ExecuteProcedure",
                QueryParameters = expectedQueryParametersWith
            };

            var expectedSqlTracesWith = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = transactionName,
                    Sql = _procNameWith,
                    DatastoreMetricName = $"Datastore/statement/MySQL/{_procNameWith.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParametersWith,
                    HasExplainPlan = false
                }
            };
            var expectedTransactionTraceQueryParametersWithout = new Assertions.ExpectedSegmentQueryParameters
            {
                segmentName = $"Datastore/statement/MySQL/{_procNameWithout.ToLower()}/ExecuteProcedure",
                QueryParameters = expectedQueryParametersWithout
            };

            var expectedSqlTracesWithout = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = transactionName,
                    Sql = _procNameWithout,
                    DatastoreMetricName = $"Datastore/statement/MySQL/{_procNameWithout.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParametersWithout,
                    HasExplainPlan = false
                }
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
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParametersWith, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTracesWith, sqlTraces),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParametersWithout, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTracesWithout, sqlTraces)
            );

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsFW462 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlStoredProcedureTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsFW471 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlStoredProcedureTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsFW48 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MySqlStoredProcedureTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsFWLatest : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlStoredProcedureTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsCoreOldest : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MySqlStoredProcedureTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsCoreLatest : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlStoredProcedureTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }
}
