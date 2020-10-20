// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetFrameworkTest]
    public class MongoDB2_6_IndexManagerTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDB2_6ApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDB2_6ApplicationFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDB2_6_IndexManagerTests(RemoteServiceFixtures.MongoDB2_6ApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.CreateOne();
                    _fixture.CreateOneAsync();
                    _fixture.CreateMany();
                    _fixture.CreateManyAsync();
                    _fixture.DropAll();
                    _fixture.DropAllAsync();
                    _fixture.DropOne();
                    _fixture.DropOneAsync();
                    _fixture.List();
                    _fixture.ListAsync();
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
        public void CreateOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/CreateOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void CreateOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/CreateOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void CreateMany()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/CreateMany");
            Assert.NotNull(m);
        }

        [Fact]
        public void CreateManyAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/CreateManyAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void DropAll()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DropAll");
            Assert.NotNull(m);
        }

        [Fact]
        public void DropAllAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DropAllAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void DropOne()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DropOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void DropOneAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/DropOneAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void List()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/List");
            Assert.NotNull(m);
        }

        [Fact]
        public void ListAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/ListAsync");
            Assert.NotNull(m);
        }
    }
}
