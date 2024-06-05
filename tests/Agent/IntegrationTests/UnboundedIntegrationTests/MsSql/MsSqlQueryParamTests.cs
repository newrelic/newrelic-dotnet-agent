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
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlQueryParamTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly string _expectedAsyncTransactionName;
        private readonly bool _paramsWithAtSigns;

        public MsSqlQueryParamTestsBase(TFixture fixture, ITestOutputHelper output, string excerciserName, bool paramsWithAtSign) : base(fixture)
        {
            MsSqlWarmupHelper.WarmupMsSql();

            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlWithParameterizedQuery";
            _expectedAsyncTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql.{excerciserName}/MsSqlAsync_WithParameterizedQuery";

            _fixture.AddCommand($"{excerciserName} MsSqlWithParameterizedQuery {paramsWithAtSign}");
            _fixture.AddCommand($"{excerciserName} MsSqlAsync_WithParameterizedQuery {paramsWithAtSign}");

            _paramsWithAtSigns = paramsWithAtSign;

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
                    configModifier.SetLogLevel("finest");       //This has to stay at finest to ensure parameter check security


                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "datastoreTracer", "queryParameters" }, "enabled", "true");

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
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = 2},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedTransactionName},
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedAsyncTransactionName},
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = _expectedTransactionName },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = _expectedAsyncTransactionName },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MSSQL/teammembers/select"
            };


            var expectedQueryParameters = _paramsWithAtSigns
                ? new Dictionary<string, object> { { "@FN", "O'Keefe" } }
                : new Dictionary<string, object> { { "FN", "O'Keefe" } };
            var expectedAsyncQueryParameters = _paramsWithAtSigns
                ? new Dictionary<string, object> { { "@LN", "Lee" } }
                : new Dictionary<string, object> { { "LN", "Lee" } };

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/teammembers/select", QueryParameters = expectedQueryParameters };
            var expectedAsyncTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/teammembers/select", QueryParameters = expectedAsyncQueryParameters };

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedTransactionName,
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                    QueryParameters = expectedQueryParameters,
                    HasExplainPlan = true
                },
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = _expectedAsyncTransactionName,
                    Sql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE LastName = @LN",
                    DatastoreMetricName = "Datastore/statement/MSSQL/teammembers/select",
                    QueryParameters = expectedAsyncQueryParameters,
                    HasExplainPlan = true
                },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var syncTransactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var asyncTransactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedAsyncTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
            var asyncTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedAsyncTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
            var logEntries = _fixture.AgentLog.GetFileLines();

            // some variable expectations based on which transaction was sampled
            var asyncSampled = asyncTransactionSample != null;
            var transactionSample = asyncSampled ? asyncTransactionSample : syncTransactionSample;
            var whereClause = asyncSampled ? "LastName = @LN" : "FirstName = @FN";
            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan" },
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = $"SELECT * FROM NewRelic.dbo.TeamMembers WHERE {whereClause}"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"},
            };

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assert.NotNull(transactionEvent),
                () => Assert.NotNull(asyncTransactionEvent)
                );

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample),

                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),
                () => Assertions.TransactionTraceSegmentQueryParametersExist(asyncSampled ? expectedAsyncTransactionTraceQueryParameters: expectedTransactionTraceQueryParameters, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
            );
        }
    }

    #region System.Data (.NET Framework only)
    [NetFrameworkTest]
    public class MsSqlQueryParamTests_SystemData_FWLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamTests_SystemData_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamTests_SystemData_NoAtSigns_FWLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamTests_SystemData_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataExerciser",
                  paramsWithAtSign: false)
        {
        }
    }
    #endregion

    #region System.Data.SqlClient (.NET Core/5+ only)

    [NetCoreTest]
    public class MsSqlQueryParamTests_SystemDataSqlClient_CoreOldest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamTests_SystemDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamTests_SystemDataSqlClient_NoAtSigns_CoreOldest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamTests_SystemDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "SystemDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }


    #endregion

    #region Microsoft.Data.SqlClient (FW and Core/5+)

    [NetFrameworkTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_FWLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_FW462 : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_FW462 : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)

            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_CoreLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_CoreOldest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: true)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }

    [NetCoreTest]
    public class MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest : MsSqlQueryParamTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MsSqlQueryParamTests_MicrosoftDataSqlClient_NoAtSigns_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(
                  fixture: fixture,
                  output: output,
                  excerciserName: "MicrosoftDataSqlClientExerciser",
                  paramsWithAtSign: false)
        {
        }
    }
    #endregion
}
