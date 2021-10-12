// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    abstract public class MongoDB2_6_MongoQueryProviderTests<T> : NewRelicIntegrationTest<T>
        where T : RemoteServiceFixtures.MongoDB2_6ApplicationFixture
    {
        private readonly RemoteServiceFixtures.MongoDB2_6ApplicationFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDB2_6_MongoQueryProviderTests(T fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.ExecuteModel();
                    _fixture.ExecuteModelAsync();
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

    [NetFrameworkTest]
    public class MongoDB2_6_FrameworkMongoQueryProviderTests : MongoDB2_6_MongoQueryProviderTests<RemoteServiceFixtures.MongoDB2_6FrameworkApplicationFixture>
    {
        public MongoDB2_6_FrameworkMongoQueryProviderTests(RemoteServiceFixtures.MongoDB2_6FrameworkApplicationFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDB2_6_CoreMongoQueryProviderTests : MongoDB2_6_MongoQueryProviderTests<RemoteServiceFixtures.MongoDB2_6CoreApplicationFixture>
    {
        public MongoDB2_6_CoreMongoQueryProviderTests(RemoteServiceFixtures.MongoDB2_6CoreApplicationFixture fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }
}
