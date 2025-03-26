// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetFrameworkTest]
    public class MongoDBTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDbApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDbApplicationFixture _fixture;

        public MongoDBTests(RemoteServiceFixtures.MongoDbApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                },
                exerciseApplication: () =>
                {
                    _fixture.Insert();
                    _fixture.Find();
                    _fixture.FindOne();
                    _fixture.FindAll();
                    _fixture.OrderedBulkInsert();
                    _fixture.Update();
                    _fixture.Remove();
                    _fixture.RemoveAll();
                    _fixture.Drop();
                    _fixture.FindAndModify();
                    _fixture.FindAndRemove();
                    _fixture.CreateIndex();
                    _fixture.GetIndexes();
                    _fixture.IndexExistsByName();
                    _fixture.Aggregate();
                    _fixture.Validate();
                    _fixture.ParallelScanAs();
                    _fixture.CreateCollection();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void TestInsert()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Insert");
            Assert.NotNull(m);
        }
        [Fact]
        public void TestFind()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Find");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestFindOne()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/FindOne");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestFindAll()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/FindAll");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestOrderedBulkInsert()
        {
            var initialize = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/InitializeOrderedBulkOperation");
            var insert = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/BulkWriteOperation Insert");
            var execute = _fixture.AgentLog.GetMetricByName("Datastore/operation/MongoDB/BulkWriteOperation Execute");

            NrAssert.Multiple(
                () => Assert.NotNull(initialize),
                () => Assert.NotNull(insert),
                () => Assert.NotNull(execute)
            );
        }

        [Fact]
        public void TestUpdate()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Update");
            Assert.NotNull(m);
        }


        [Fact]
        public void TestRemove()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Remove");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestRemoveAll()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/RemoveAll");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestDrop()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Drop");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestFindAndModify()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/FindAndModify");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestFindAndRemove()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/FindAndRemove");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestCreateIndex()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/CreateIndex");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestGetIndexes()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/GetIndexes");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestIndexExistsByName()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/IndexExistsByName");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestAggregate()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Aggregate");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestValidate()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/Validate");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestParallelScanas()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/ParallelScanAs");
            Assert.NotNull(m);
        }

        [Fact]
        public void TestCreateCollection()
        {
            var m = _fixture.AgentLog.GetMetricByName("Datastore/statement/MongoDB/myCollection/CreateCollection");
            Assert.NotNull(m);
        }

    }
}
