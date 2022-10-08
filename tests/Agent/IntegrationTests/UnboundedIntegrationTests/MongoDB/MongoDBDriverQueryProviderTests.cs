// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverQueryProviderTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverQueryProviderTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
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
    public class MongoDBDriverQueryProviderTestsFWLatest : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverQueryProviderTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW471 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverQueryProviderTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW462 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverQueryProviderTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverQueryProviderTestsCoreLatest : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverQueryProviderTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverQueryProviderTestsCore50 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MongoDBDriverQueryProviderTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverQueryProviderTestsCore31 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MongoDBDriverQueryProviderTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
