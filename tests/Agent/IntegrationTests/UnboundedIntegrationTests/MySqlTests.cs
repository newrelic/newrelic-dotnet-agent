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
    public class MySqlTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public MySqlTests(RemoteServiceFixtures.BasicMvcApplication fixture, ITestOutputHelper output)
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

                    var instrumentationFilePath = string.Format(@"{0}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", fixture.DestinationNewRelicExtensionsDirectoryPath);
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "", "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetMySql();
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
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/MySQL/dates/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MySql"},

                // We are not checking callCount on Iterate metrics because they can be confusing in that calls like Open can result in calls to Read.
                // This is particularly true for MySQL, but doing this for all vendors for consistency.
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate" },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", metricScope = "WebTransaction/MVC/DefaultController/MySql"}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // The datastore operation happened inside a web transaction so there should be no allOther metrics
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/MySQL/allOther", callCount = 1 },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/MySQL/select", callCount = 1, metricScope = "WebTransaction/MVC/DefaultController/MySql" }
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
                    TransactionName = "WebTransaction/MVC/DefaultController/MySql",
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
            var transactionSample = _fixture.AgentLog.TryGetTransactionSample("WebTransaction/MVC/DefaultController/MySql");
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/MySql");
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
