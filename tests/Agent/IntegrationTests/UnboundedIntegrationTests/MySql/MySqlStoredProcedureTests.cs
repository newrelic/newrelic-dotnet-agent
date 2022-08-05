// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public abstract class MySqlStoredProcedureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly bool _paramsWithAtSigns;
        private string _procedureName = "testProcedure" + Guid.NewGuid().ToString("n").Substring(0, 4);

        protected MySqlStoredProcedureTestsBase(TFixture fixture, ITestOutputHelper output, bool paramsWithAtSigns) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _paramsWithAtSigns = paramsWithAtSigns;

            _fixture.AddCommand($"MySqlExerciser CreateAndExecuteStoredProcedure {_procedureName} {paramsWithAtSigns}");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();
                    configModifier.SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                }
            );

            // Confirm transaction transform has completed before moving on to host application shutdown, and final sendDataOnExit harvest
            _fixture.AddActions(exerciseApplication: () => _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2)));

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MySql.MySqlExerciser/CreateAndExecuteStoredProcedure";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MySQL/{_procedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = transactionName}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MySQL/{_procedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = _paramsWithAtSigns
                ? DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue)
                : DbParameterData.MySqlParameters.ToDictionary(p => p.ParameterName.TrimStart('@'), p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters
            {
                segmentName = $"Datastore/statement/MySQL/{_procedureName.ToLower()}/ExecuteProcedure",
                QueryParameters = expectedQueryParameters
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = transactionName,
                    Sql = _procedureName,
                    DatastoreMetricName = $"Datastore/statement/MySQL/{_procedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters,
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
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithAtSignsFW462 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlStoredProcedureTestsWithAtSignsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsFW462 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithAtSignsFW471 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlStoredProcedureTestsWithAtSignsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsFW471 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithAtSignsFWLatest : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlStoredProcedureTestsWithAtSignsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetFrameworkTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsFWLatest : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithAtSignsCore31 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MySqlStoredProcedureTestsWithAtSignsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsCore31 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithAtSignsCore50 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MySqlStoredProcedureTestsWithAtSignsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsCore50 : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithAtSignsCore : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlStoredProcedureTestsWithAtSignsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output, true)
        {

        }
    }

    [NetCoreTest]
    public class MySqlStoredProcedureTestsWithoutAtSignsCore : MySqlStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MySqlStoredProcedureTestsWithoutAtSignsCore(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output, false)
        {

        }
    }
}
