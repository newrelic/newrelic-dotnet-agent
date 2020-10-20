// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MsSql
{
    public abstract class MsSqlQueryParameterCaptureTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture:RemoteApplicationFixture, RemoteServiceFixtures.IMsSqlClientFixture
    {
        private readonly RemoteServiceFixtures.IMsSqlClientFixture _fixture;
        private readonly string _expectedTransactionName;
        private readonly bool _paramsWithAtSigns;

        public MsSqlQueryParameterCaptureTestsBase(TFixture fixture, ITestOutputHelper output, string expectedTransactionName, bool paramsWithAtSigns) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _expectedTransactionName = expectedTransactionName;
            _paramsWithAtSigns = paramsWithAtSigns;


            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

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
                    _fixture.GetMsSql_WithParameterizedQuery(_paramsWithAtSigns);
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/all", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allWeb", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MSSQL/{CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)}/default", callCount = 1},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/statement/MSSQL/teammembers/select", callCount = 1, metricScope = _expectedTransactionName},
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				// The datastore operation happened inside a web transaction so there should be no allOther metrics
				new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/MSSQL/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MSSQL/select", metricScope = _expectedTransactionName },
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                $"Datastore/statement/MSSQL/teammembers/select"
            };


            var expectedQueryParameters = _paramsWithAtSigns
                ? new Dictionary<string, object> { { "@FN", "O'Keefe" } }
                : new Dictionary<string, object> { { "FN", "O'Keefe" } };


            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "explain_plan" },
                new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/statement/MSSQL/teammembers/select", parameterName = "sql", parameterValue = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "host", parameterValue = CommonUtils.NormalizeHostname(MsSqlConfiguration.MsSqlServer)},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "port_path_or_id", parameterValue = "default"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MSSQL/teammembers/select", parameterName = "database_name", parameterValue = "NewRelic"},
            };

            var expectedTransactionTraceQueryParameters = new Assertions.ExpectedSegmentQueryParameters { segmentName = $"Datastore/statement/MSSQL/teammembers/select", QueryParameters = expectedQueryParameters };

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
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample(_expectedTransactionName);
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_expectedTransactionName);
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
            var logEntries = _fixture.AgentLog.GetFileLines();

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
                () => Assertions.TransactionTraceSegmentQueryParametersExist(expectedTransactionTraceQueryParameters, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.LogLinesNotExist(new[] { AgentLogFile.ErrorLogLinePrefixRegex }, logEntries)
            );
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParameterCaptureTests : MsSqlQueryParameterCaptureTestsBase<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        public MsSqlQueryParameterCaptureTests(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MsSqlController/MsSql_WithParameterizedQuery", true)
        {
        }
    }

    [NetFrameworkTest]
    public class MsSqlQueryParameterCaptureTests_ParamsWithoutAtSigns : MsSqlQueryParameterCaptureTestsBase<RemoteServiceFixtures.MsSqlBasicMvcFixture>
    {
        public MsSqlQueryParameterCaptureTests_ParamsWithoutAtSigns(RemoteServiceFixtures.MsSqlBasicMvcFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MsSqlController/MsSql_WithParameterizedQuery", false)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftDataSqlClientQueryParameterCaptureTests : MsSqlQueryParameterCaptureTestsBase<RemoteServiceFixtures.MicrosoftDataSqlClientFixture>
    {
        public MicrosoftDataSqlClientQueryParameterCaptureTests(RemoteServiceFixtures.MicrosoftDataSqlClientFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MicrosoftDataSqlClient/MsSql_WithParameterizedQuery/{tableName}/{paramsWithAtSign}", true)
        {
        }
    }

    [NetCoreTest]
    public class MicrosoftDataSqlClientQueryParameterCaptureTests_ParamsWithoutAtSigns : MsSqlQueryParameterCaptureTestsBase<RemoteServiceFixtures.MicrosoftDataSqlClientFixture>
    {
        public MicrosoftDataSqlClientQueryParameterCaptureTests_ParamsWithoutAtSigns(RemoteServiceFixtures.MicrosoftDataSqlClientFixture fixture, ITestOutputHelper output)
            : base(fixture, output, "WebTransaction/MVC/MicrosoftDataSqlClient/MsSql_WithParameterizedQuery/{tableName}/{paramsWithAtSign}", false)
        {
        }
    }
}
