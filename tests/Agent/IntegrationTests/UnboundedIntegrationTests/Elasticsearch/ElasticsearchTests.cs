// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.ElasticsearchTests
{


    public abstract class ElasticsearchTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        protected enum ClientType
        {
            ElasticsearchNet,
            NEST,
            ElasticClients
        }
        private readonly ConsoleDynamicMethodFixture _fixture;

        protected ElasticsearchTestsBase(TFixture fixture, ITestOutputHelper output, string clientType) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            _fixture.AddCommand($"ElasticsearchExerciser SetClient {clientType}");

            _fixture.AddCommand($"ElasticsearchExerciser Index");

            _fixture.AddCommand($"ElasticsearchExerciser Index async");

            _fixture.AddCommand($"ElasticsearchExerciser Search");

            _fixture.AddCommand($"ElasticsearchExerciser Search async");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void CreateReadAndDeleteDatabaseTests()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadFeedDatabase", metricScope = expectedTransactionName, callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var traceId = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals(expectedTransactionName)).FirstOrDefault().IntrinsicAttributes["traceId"];

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/operation/CosmosDB"));


            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Equal(6, operationDatastoreSpans.Count())
            );
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFWLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverQueryProviderTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW48 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverQueryProviderTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW471 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverQueryProviderTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestTestsFW462 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNestTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCoreLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNestTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore60 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNestTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore50 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNestTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore31 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNestTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }
}
