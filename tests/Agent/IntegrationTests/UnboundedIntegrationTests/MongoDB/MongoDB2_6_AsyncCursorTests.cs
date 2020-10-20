// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    [NetFrameworkTest]
    public class MongoDB2_6_AsyncCursorTests : NewRelicIntegrationTest<RemoteServiceFixtures.MongoDB2_6ApplicationFixture>
    {
        private readonly RemoteServiceFixtures.MongoDB2_6ApplicationFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDB2_6_AsyncCursorTests(RemoteServiceFixtures.MongoDB2_6ApplicationFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                exerciseApplication: () =>
                {
                    _fixture.MoveNext();
                    _fixture.MoveNextAsync();
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
        public void GetNextBatch()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/GetNextBatch");

            Assert.NotNull(m);
        }

        [Fact]
        public void GetNextBatchAsync()
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/GetNextBatchAsync");

            Assert.NotNull(m);
        }

    }
}
