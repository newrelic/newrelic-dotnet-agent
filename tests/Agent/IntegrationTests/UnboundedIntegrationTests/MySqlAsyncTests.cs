using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests
{
    public class MySqlAsyncTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public MySqlAsyncTests(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.SqlAsync.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(
                       instrumentationFilePath,
                        "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMySqlAsync();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The MySql driver executes a ton of unrelated queries while connecting, such as "SHOW VARIABLES"
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/all", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allWeb", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/MySQL/{MySqlTestConfiguration.MySqlServer}/{MySqlTestConfiguration.MySqlPort}", callCount = 8},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/show", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/set", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MySqlAsync"},

                // We don't currently have instrumentation for MySqlDataReader.ReadAsync
                //new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 15 },
                //new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = 15, metricScope = "WebTransaction/MVC/DefaultController/MySqlAsync"}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allOther", callCount = 1 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MySqlAsync" }
            };
            var expectedTransactionTraceSegments = new List<string>
            {
                "Datastore/statement/MySQL/dates/select"
            };

            // On hold until explain plans are supported for MySql
            //var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            //{
            //  new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "explain_plan"}
            //};

            var expectedTransactionEventIntrinsicAttributes = new List<string>
            {
                "databaseDuration"
            };
            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = "WebTransaction/MVC/DefaultController/MySqlAsync",
                    Sql = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?",
                    DatastoreMetricName = "Datastore/statement/MySQL/dates/select",
                    
                    // On hold until explain plans are supported for MySql
                    //HasExplainPlan = true
                }
            };

            var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
            {
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "sql", parameterValue = "SELECT _date FROM dates WHERE _date LIKE ? ORDER BY _date DESC LIMIT ?"},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "host", parameterValue = MySqlTestConfiguration.MySqlServer},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "port_path_or_id", parameterValue = MySqlTestConfiguration.MySqlPort},
                new Assertions.ExpectedSegmentParameter { segmentName = "Datastore/statement/MySQL/dates/select", parameterName = "database_name", parameterValue = "NewRelic"}

            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/MySqlAsync");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/MySqlAsync");
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

                // On hold until explain plans are supported for MySql
                //() => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample),

                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, transactionEvent),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
                () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample)
            );
        }
    }
}
