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

namespace NewRelic.Agent.UnboundedIntegrationTests.CosmosDB
{
    public abstract class CosmosDBTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private string UniqueDbName => "test_db_" + Guid.NewGuid().ToString("n").Substring(0, 4);
        private string _testContainerName = "testContainer";

        protected CosmosDBTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            _fixture.AddCommand($"CosmosDBExerciser StartAgent");

            _fixture.AddCommand($"CosmosDBExerciser CreateReadAndDeleteDatabase {UniqueDbName}");

            _fixture.AddCommand($"CosmosDBExerciser CreateReadAndDeleteContainers {UniqueDbName} {_testContainerName}");

            _fixture.AddCommand($"CosmosDBExerciser CreateAndReadItems {UniqueDbName} {_testContainerName}");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void CreateReadAndDeleteDatabaseTests()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadFeedDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var traceId = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase")).FirstOrDefault().IntrinsicAttributes["traceId"];

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/operation/CosmosDB"));


            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Equal(6, operationDatastoreSpans.Count())
            );
        }

        [Fact]
        public void CreateReadAndDeleteContainersTests()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/DeleteCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers", callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }

        [Fact]
        public void CreateAndReadItemsTests()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                //From calling Container.CreateItemStreamAsync() and Container.CreateItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/CreateDocument", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 2 },

                //From calling Container.UpsertItemStreamAsync() and Container.UpsertItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/UpsertDocument", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 2 },

                //From calling FeedIterator.ReadNextAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadFeedDocument", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 1 },

                //From calling Container.ReadManyItemsStreamAsync() and Container.ReadManyItemsAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/QueryDocument", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems", callCount = 2 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }


    [NetFrameworkTest]
    public class CosmosDBTestsFW : CosmosDBTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public CosmosDBTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }


    [NetCoreTest]
    public class CosmosDBTestsCore : CosmosDBTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public CosmosDBTestsCore(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
