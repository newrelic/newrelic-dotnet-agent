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
    public abstract class OracleStoredProcedureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _storedProcedureName;

        protected OracleStoredProcedureTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _storedProcedureName = GenerateStoredProcedureName();

            _fixture.AddCommand($"OracleExerciser InitializeStoredProcedure {_storedProcedureName}"); // creates a new stored procedure. The stored procedure gets dropped automatically when the exerciser goes out of scope)
            _fixture.AddCommand($"OracleExerciser ExerciseStoredProcedure"); 

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
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2));
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
                new() { metricName = $@"Datastore/statement/Oracle/{_storedProcedureName.ToLower()}/ExecuteProcedure", callCount = 1 },
                new() { metricName = $@"Datastore/statement/Oracle/{_storedProcedureName.ToLower()}/ExecuteProcedure", callCount = 1, metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseStoredProcedure"}
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/Oracle/{_storedProcedureName.ToLower()}/ExecuteProcedure"
            };

            var expectedQueryParameters = DbParameterData.OracleParameters.ToDictionary(p => p.ParameterName, p => p.ExpectedValue);

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/Oracle/{_storedProcedureName.ToLower()}/ExecuteProcedure", QueryParameters = expectedQueryParameters };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new()
                {
                    TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseStoredProcedure",
                    Sql = _storedProcedureName,
                    DatastoreMetricName = $"Datastore/statement/Oracle/{_storedProcedureName.ToLower()}/ExecuteProcedure",
                    QueryParameters = expectedQueryParameters,
                    HasExplainPlan = false
                }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseStoredProcedure");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Oracle.OracleExerciser/ExerciseStoredProcedure");
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

            Assert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent)
            );

            Assert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }

        private string GenerateStoredProcedureName()
        {
            return $"OracleStoredProcedureTest{Guid.NewGuid():N}".Substring(0, 30);
        }
    }

    [NetFrameworkTest]
    public class OracleStoredProcedureTestsFramework462 : OracleStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public OracleStoredProcedureTestsFramework462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }


    [NetFrameworkTest]
    public class OracleStoredProcedureTestsFramework471 : OracleStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public OracleStoredProcedureTestsFramework471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
    [NetFrameworkTest]
    public class OracleStoredProcedureTestsFrameworkLatest : OracleStoredProcedureTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public OracleStoredProcedureTestsFrameworkLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class OracleStoredProcedureTestsCoreLatest : OracleStoredProcedureTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public OracleStoredProcedureTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
