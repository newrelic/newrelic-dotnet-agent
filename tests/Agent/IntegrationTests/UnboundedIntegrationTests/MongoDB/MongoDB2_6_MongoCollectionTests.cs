// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetFrameworkTest]
    public class MongoDB2_6_MongoCollectionTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDB2_6ApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDB2_6ApplicationFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDB2_6_MongoCollectionTests(RemoteServiceFixtures.MongoDB2_6ApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.Count();
                    _fixture.CountAsync();
                    _fixture.Distinct();
                    _fixture.DistinctAsync();
                    _fixture.MapReduce();
                    _fixture.MapReduceAsync();
                    _fixture.Watch();
                    _fixture.WatchAsync();

                    _fixture.InsertOne();
                    _fixture.InsertOneAsync();
                    _fixture.InsertMany();
                    _fixture.InsertManyAsync();
                    _fixture.ReplaceOne();
                    _fixture.ReplaceOneAsync();
                    _fixture.UpdateOne();
                    _fixture.UpdateOneAsync();
                    _fixture.UpdateMany();
                    _fixture.UpdateManyAsync();
                    _fixture.DeleteOne();
                    _fixture.DeleteOneAsync();
                    _fixture.DeleteMany();
                    _fixture.DeleteManyAsync();
                    _fixture.FindSync();
                    _fixture.FindAsync();
                    _fixture.FindOneAndDelete();
                    _fixture.FindOneAndDeleteAsync();
                    _fixture.FindOneAndReplace();
                    _fixture.FindOneAndReplaceAsync();
                    _fixture.FindOneAndUpdate();
                    _fixture.FindOneAndUpdateAsync();
                    _fixture.BulkWrite();
                    _fixture.BulkWriteAsync();
                    _fixture.Aggregate();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void CheckForDatastoreInstanceMetrics()
        {
            var serverHost = CommonUtils.NormalizeHostname(MongoDbConfiguration.MongoDb26Server);
            var m = _fixture.AgentLog.GetMetricByName($"Datastore/instance/MongoDB/{serverHost}/{MongoDbConfiguration.MongoDb26Port}");
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
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Watch");
            Assert.NotNull(m);
        }

        [Fact]
        public void WatchAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/WatchAsync");
            Assert.NotNull(m);
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
}
