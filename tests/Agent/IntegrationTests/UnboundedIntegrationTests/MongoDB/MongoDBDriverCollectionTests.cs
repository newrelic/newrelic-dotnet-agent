// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverCollectionTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private bool _clientSupportsWatchMethods;

        private string _mongoUrl;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverCollectionTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl, bool clientSupportsWatchMethods = true) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;
            _clientSupportsWatchMethods = clientSupportsWatchMethods;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            _fixture.AddCommand("MongoDbDriverExerciser Count");
            _fixture.AddCommand("MongoDbDriverExerciser CountAsync");
            _fixture.AddCommand("MongoDbDriverExerciser Distinct");
            _fixture.AddCommand("MongoDbDriverExerciser DistinctAsync");
            _fixture.AddCommand("MongoDbDriverExerciser MapReduce");
            _fixture.AddCommand("MongoDbDriverExerciser MapReduceAsync");

            // watch and watchasync are unavailable in MongoDB.Driver version 2.3
            if (_clientSupportsWatchMethods)
            {
                _fixture.AddCommand("MongoDbDriverExerciser Watch");
                _fixture.AddCommand("MongoDbDriverExerciser WatchAsync");
            }

            _fixture.AddCommand("MongoDbDriverExerciser InsertOne");
            _fixture.AddCommand("MongoDbDriverExerciser InsertOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser InsertMany");
            _fixture.AddCommand("MongoDbDriverExerciser InsertManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser ReplaceOne");
            _fixture.AddCommand("MongoDbDriverExerciser ReplaceOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateOne");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateMany");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteOne");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteMany");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindSync");
            _fixture.AddCommand("MongoDbDriverExerciser FindAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndDelete");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndDeleteAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndReplace");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndReplaceAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndUpdate");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndUpdateAsync");
            _fixture.AddCommand("MongoDbDriverExerciser BulkWrite");
            _fixture.AddCommand("MongoDbDriverExerciser BulkWriteAsync");
            _fixture.AddCommand("MongoDbDriverExerciser Aggregate");

            _fixture.Initialize();
        }

        [Fact]
        public void CheckForDatastoreInstanceMetrics()
        {
            var mongoUri = new UriBuilder(_mongoUrl);
            var serverHost = CommonUtils.NormalizeHostname(mongoUri.Host);
            var m = _fixture.AgentLog.GetMetricByName($"Datastore/instance/MongoDB/{serverHost}/{mongoUri.Port}");
            Assert.NotNull(m);
        }

        [Fact]
        public void Count()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Count");
            Assert.NotNull(m);
        }

        [Fact]
        public void CountAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/CountAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void Distinct()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Distinct");
            Assert.NotNull(m);
        }

        [Fact]
        public void DistinctAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DistinctAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void MapReduce()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/MapReduce");
            Assert.NotNull(m);
        }

        [Fact]
        public void MapReduceAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/MapReduceAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void Watch()
        {
            if (_clientSupportsWatchMethods)
            {
                var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Watch");
                Assert.NotNull(m);
            }
        }

        [Fact]
        public void WatchAsync()
        {
            if (_clientSupportsWatchMethods)
            {
                var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/WatchAsync");
                Assert.NotNull(m);
            }
        }

        [Fact]
        public void InsertOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/InsertOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void InsertOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/InsertOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void InsertMany()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/InsertMany");
            Assert.NotNull(m);
        }

        [Fact]
        public void InsertManyAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/InsertManyAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void ReplaceOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/ReplaceOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void ReplaceOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/ReplaceOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void UpdateOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/UpdateOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void UpdateOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/UpdateOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void UpdateMany()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/UpdateMany");
            Assert.NotNull(m);
        }

        [Fact]
        public void UpdateManyAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/UpdateManyAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void DeleteOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DeleteOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void DeleteOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DeleteOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void DeleteMany()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DeleteMany");
            Assert.NotNull(m);
        }

        [Fact]
        public void DeleteManyAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DeleteManyAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindSync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindSync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndDelete()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndDelete");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndDeleteAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndDeleteAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndReplace()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndReplace");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndReplaceAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndReplaceAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndUpdate()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndUpdate");
            Assert.NotNull(m);
        }

        [Fact]
        public void FindOneAndUpdateAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/FindOneAndUpdateAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void BulkWrite()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/BulkWrite");
            Assert.NotNull(m);
        }

        [Fact]
        public void BulkWriteAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/BulkWriteAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void Aggregate()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Aggregate");
            Assert.NotNull(m);
        }

    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFWLatest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverCollectionTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW48 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverCollectionTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW471 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverCollectionTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW462 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverCollectionTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            // 2.3 doesn't support the Watch/WatchAsync methods
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString, false)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverCollectionTestsCoreLatest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverCollectionTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverCollectionTestsCoreOldest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverCollectionTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

}
