// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetCoreTest]
    public class MongoDB2_6_CoreSmokeTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDB2_6CoreApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDB2_6CoreApplicationFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCoreCollection";

        public MongoDB2_6_CoreSmokeTests(RemoteServiceFixtures.MongoDB2_6CoreApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.InsertOne();
                    _fixture.InsertOneAsync();
                    _fixture.UpdateOne();
                    _fixture.UpdateOneAsync();
                    _fixture.DeleteOne();
                    _fixture.DeleteOneAsync();
                    _fixture.ExecuteModel();
                    _fixture.ExecuteModelAsync();
                }
            );

            _fixture.Initialize();
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
        public void ExecuteModel()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/LinqQuery");

            Assert.NotNull(m);
        }

        [Fact]
        public void ExecuteModelAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/LinqQueryAsync");
            Assert.NotNull(m);
        }
    }
}
