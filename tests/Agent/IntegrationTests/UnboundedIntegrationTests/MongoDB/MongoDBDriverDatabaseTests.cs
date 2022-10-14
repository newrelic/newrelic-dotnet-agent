// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverDatabaseTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private string _mongoUrl;

        public MongoDBDriverDatabaseTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            _fixture.AddCommand("MongoDBDriverExerciser CreateCollection");
            _fixture.AddCommand("MongoDBDriverExerciser CreateCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser DropCollection");
            _fixture.AddCommand("MongoDBDriverExerciser DropCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser ListCollections");
            _fixture.AddCommand("MongoDBDriverExerciser ListCollectionsAsync");
            _fixture.AddCommand("MongoDBDriverExerciser RenameCollection");
            _fixture.AddCommand("MongoDBDriverExerciser RenameCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser RunCommand");
            _fixture.AddCommand("MongoDBDriverExerciser RunCommandAsync");

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

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFWLatest : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverDatabaseTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFW471 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverDatabaseTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFW462 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverDatabaseTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverDatabaseTestsCoreLatest : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverDatabaseTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverDatabaseTestsCore50 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MongoDBDriverDatabaseTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverDatabaseTestsCore31 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MongoDBDriverDatabaseTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

}
