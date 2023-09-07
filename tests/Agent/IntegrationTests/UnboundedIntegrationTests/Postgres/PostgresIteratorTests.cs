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

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres
{
    public abstract class PostgresSqlIteratorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        public PostgresSqlIteratorTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"PostgresSqlExerciser IteratorTest");

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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "DataReaderTracer", "enabled", "true");
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
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/IteratorTest";
            var expectedDatastoreCallCount = 1;

            //These values are dictated by the queries that are being run as part of this test.
            //The typical pattern in this case is for there to be a call to Read(), followed by a call to NextResult(), followed by a final call to
            //Read() which returns false to exit the loop.  Each of these roll up to Iterate for a total of 3
            var expectedIterationCount = 3;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allOther", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/instance/Postgres/{CommonUtils.NormalizeHostname(PostgresConfiguration.PostgresServer)}/{PostgresConfiguration.PostgresPort}", callCount = expectedDatastoreCallCount},
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = @"Datastore/statement/Postgres/teammembers/select", callCount = 1, metricScope = expectedTransactionName},

                // NpgsqlDataReader methods Read/ReadAsync and NextResult/NextResultAsync result in Iterate metrics.
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterationCount },
                new Assertions.ExpectedMetric { metricName = @"DotNet/DatabaseResult/Iterate", callCount = expectedIterationCount, metricScope = expectedTransactionName}
            };
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Postgres/allWeb" },

                // The operation metric should not be scoped because the statement metric is scoped instead
                new Assertions.ExpectedMetric { metricName = @"Datastore/operation/Postgres/select", metricScope = expectedTransactionName }
            };
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );
        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorTestsFW462 : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public PostgresSqlIteratorTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorTestsFW471 : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public PostgresSqlIteratorTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorTestsFW48 : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public PostgresSqlIteratorTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorTestsFWLatest : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public PostgresSqlIteratorTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorTestsCoreOldest : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public PostgresSqlIteratorTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorTestsCoreLatest : PostgresSqlIteratorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public PostgresSqlIteratorTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

}
