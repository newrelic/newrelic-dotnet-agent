// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetFrameworkTest]
    public class MongoDB2_6_DatabaseTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDB2_6ApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDB2_6ApplicationFixture _fixture;

        public MongoDB2_6_DatabaseTests(RemoteServiceFixtures.MongoDB2_6ApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (

                exerciseApplication: () =>
                {
                    _fixture.CreateCollection();
                    _fixture.CreateCollectionAsync();
                    _fixture.DropCollection();
                    _fixture.DropCollectionAsync();
                    _fixture.ListCollections();
                    _fixture.ListCollectionsAsync();
                    _fixture.RenameCollection();
                    _fixture.RenameCollectionAsync();
                    _fixture.RunCommand();
                    _fixture.RunCommandAsync();
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
        public void CreateCollection()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/createTestCollection/CreateCollection");

            Assert.NotNull(m);
        }

        [Fact]
        public void CreateCollectionAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/createTestCollectionAsync/CreateCollectionAsync");

            Assert.NotNull(m);
        }

        [Fact]
        public void DropCollection()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/dropTestCollection/DropCollection");

            Assert.NotNull(m);
        }

        [Fact]
        public void DropCollectionAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/dropTestCollectionAsync/DropCollectionAsync");

            Assert.NotNull(m);
        }

        [Fact]
        public void ListCollections()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/ListCollections");

            Assert.NotNull(m);
        }

        [Fact]
        public void ListCollectionsAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/ListCollectionsAsync");

            Assert.NotNull(m);
        }

        [Fact]
        public void RenameCollection()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/RenameCollection");

            Assert.NotNull(m);
        }

        [Fact]
        public void RenameCollectionAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/RenameCollectionAsync");

            Assert.NotNull(m);
        }

        [Fact]
        public void RunCommand()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/RunCommand");

            Assert.NotNull(m);
        }

        [Fact]
        public void RunCommandAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/RunCommandAsync");

            Assert.NotNull(m);
        }



    }
}
