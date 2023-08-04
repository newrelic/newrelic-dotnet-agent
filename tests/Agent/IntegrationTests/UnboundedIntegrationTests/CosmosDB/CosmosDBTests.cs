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

        protected CosmosDBTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            _fixture.AddCommand($"CosmosDBExerciser StartAgent");
            _fixture.AddCommand($"CosmosDBExerciser CreateReadAndDeleteDatabase {UniqueDbName}");
            _fixture.AddCommand($"CosmosDBExerciser CreateReadAndDeleteContainers {UniqueDbName} {_testContainerName}");
            _fixture.AddCommand($"CosmosDBExerciser CreateAndReadItems {UniqueDbName} {_testContainerName}");
            _fixture.AddCommand($"CosmosDBExerciser CreateAndQueryItems {UniqueDbName} {_testContainerName}");

            var itemsToCreate = 20;
            _fixture.AddCommand($"CosmosDBExerciser CreateItemsConcurrentlyAsync {UniqueDbName} {_testContainerName} {itemsToCreate}");
            _fixture.AddCommand($"CosmosDBExerciser CreateAndExecuteStoredProc {UniqueDbName} {_testContainerName}");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterSqlTracesHarvestCycle(15);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(15);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainEnabled", "true");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SqlTraceDataLogLineRegex, TimeSpan.FromMinutes(1));
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

        [Fact]
        public void CreateReadAndDeleteContainersTests()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteContainers";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/DeleteCollection", metricScope = expectedTransactionName, callCount = 1 }
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
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndReadItems";
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = expectedTransactionName, callCount = 1 },

                //From calling Container.CreateItemStreamAsync() and Container.CreateItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/CreateDocument", metricScope = expectedTransactionName, callCount = 2 },

                //From calling Container.UpsertItemStreamAsync() and Container.UpsertItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/UpsertDocument", metricScope = expectedTransactionName, callCount = 2 },

                //From calling FeedIterator.ReadNextAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadFeedDocument", metricScope = expectedTransactionName, callCount = 1 },

                //From calling Container.ReadManyItemsStreamAsync() and Container.ReadManyItemsAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/QueryDocument", metricScope = expectedTransactionName, callCount = 2 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }

        [Fact]
        public void CreateAndQueryItemsTests()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndQueryItems";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = expectedTransactionName, callCount = 1 },

                //From calling Container.CreateItemStreamAsync() and Container.CreateItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/CreateDocument", metricScope = expectedTransactionName, callCount = 2 },

                //From calling Container.UpsertItemStreamAsync() and Container.UpsertItemAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/UpsertDocument", metricScope = expectedTransactionName, callCount = 2 },

                //From calling Container.GetItemQueryIterator() and Container.GetItemQueryStreamIterator()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/QueryDocument", metricScope = expectedTransactionName, callCount = 2 }
            };

            var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
            {
                new Assertions.ExpectedSqlTrace
                {
                    TransactionName = expectedTransactionName,
                    Sql = "SELECT * FROM SalesOrders s WHERE s.AccountNumber = ? AND s.TotalDue > ?",
                    DatastoreMetricName = $"Datastore/statement/CosmosDB/{_testContainerName}/QueryDocument"
                },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var sqlTraces = _fixture.AgentLog.GetSqlTraces().Where(st => st.TransactionName == expectedTransactionName);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces)
            );
        }

        [Fact]
        public void BulkCreatingItemsTests()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateItemsConcurrentlyAsync";
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = expectedTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
        }

        [Fact]
        public void CreateAndExecuteStoredProcTests()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateAndExecuteStoredProc";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = expectedTransactionName, callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ReadCollection", metricScope = expectedTransactionName, callCount = 1 },

                //From calling Scripts.CreateStoredProcedureAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/CreateStoredProcedure", metricScope = expectedTransactionName, callCount = 1 },

                //From calling Scripts.ExecuteStoredProcedureAsync()
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/CosmosDB/{_testContainerName}/ExecuteJavaScriptStoredProcedure", metricScope = expectedTransactionName, callCount = 1 }
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            Assertions.MetricsExist(expectedMetrics, metrics);
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
    public class CosmosDBTestsCore : CosmosDBTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public CosmosDBTestsCore(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
