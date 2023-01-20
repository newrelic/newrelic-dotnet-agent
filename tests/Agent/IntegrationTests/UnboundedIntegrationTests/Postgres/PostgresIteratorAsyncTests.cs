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

namespace NewRelic.Agent.UnboundedIntegrationTests.Postgres
{
    public abstract class PostgresSqlIteratorAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        public PostgresSqlIteratorAsyncTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"PostgresSqlExerciser AsyncIteratorTest");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces()
                    .SetLogLevel("finest");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml";
                    CommonUtils.SetAttributeOnTracerFactoryInNewRelicInstrumentation(instrumentationFilePath, "DataReaderTracer", "enabled", "true");
                }
            );

            _fixture.AddActions(exerciseApplication: () => _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(2)));

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.PostgresSql.PostgresSqlExerciser/AsyncIteratorTest";
            var expectedDatastoreCallCount = 1;

            //These values are dictated by the queries that are being run as part of this test.
            //There are two application endpoints being exercised by the test, each of which runs a query that returns a single row.
            //The typical pattern in this case is for there to be a call to Read(), followed by a call to NextResult(), followed by a final call to
            //Read() which returns false to exit the loop.  Each of these roll up to Iterate for a total of 3 for each endpoint.
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
    public class PostgresSqlIteratorAsyncTestsFW462 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public PostgresSqlIteratorAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorAsyncTestsFW471 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public PostgresSqlIteratorAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorAsyncTestsFW48 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public PostgresSqlIteratorAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetFrameworkTest]
    public class PostgresSqlIteratorAsyncTestsFWLatest : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public PostgresSqlIteratorAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorAsyncTestsCore31 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public PostgresSqlIteratorAsyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorAsyncTestsCore50 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public PostgresSqlIteratorAsyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorAsyncTestsCore60 : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public PostgresSqlIteratorAsyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

    [NetCoreTest]
    public class PostgresSqlIteratorAsyncTestsCoreLatest : PostgresSqlIteratorAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public PostgresSqlIteratorAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {

        }
    }

}
