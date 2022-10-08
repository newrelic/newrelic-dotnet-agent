// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverMongoQueryProviderTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverMongoQueryProviderTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand("MongoDBDriverExerciser ExecuteModel");
            _fixture.AddCommand("MongoDBDriverExerciser ExecuteModelAsync");

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
            // Starting with driver version 2.14.0, the operation name we generate for
            // "Collection.AsQueryable()" changed from LinqQuery to Aggregate
            // TODO: figure out if this is a bug
            // TODO: figure out a cleaner way of handling this difference
            var linqQueryMetric = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/LinqQuery");
            var aggregateMetric = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/Aggregate");

            Assert.True(linqQueryMetric != null || aggregateMetric != null);
        }

        [Fact]
        public void ExecuteModelAsync()
        {
            // See comments for ExecuteModel above
            var linqQueryMetric = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/LinqQueryAsync");
            var aggregateMetric = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/AggregateAsync");

            Assert.True(linqQueryMetric != null || aggregateMetric != null);
        }

    }

    [NetFrameworkTest]
    public class MongoDBDriverMongoQueryProviderTestsFWLatest : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverMongoQueryProviderTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverMongoQueryProviderTestsFW471 : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverMongoQueryProviderTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverMongoQueryProviderTestsFW462 : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverMongoQueryProviderTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverMongoQueryProviderTestsCoreLatest : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverMongoQueryProviderTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverMongoQueryProviderTestsCore50 : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MongoDBDriverMongoQueryProviderTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverMongoQueryProviderTestsCore31 : MongoDBDriverMongoQueryProviderTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MongoDBDriverMongoQueryProviderTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
