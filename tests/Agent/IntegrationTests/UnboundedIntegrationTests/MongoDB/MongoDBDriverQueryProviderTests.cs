// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverQueryProviderTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private string _mongoUrl;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverQueryProviderTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            _fixture.AddCommand("MongoDBDriverExerciser ExecuteModel");
            _fixture.AddCommand("MongoDBDriverExerciser ExecuteModelAsync");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

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
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW48 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverQueryProviderTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW471 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverQueryProviderTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverQueryProviderTestsFW462 : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverQueryProviderTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverQueryProviderTestsCoreLatest : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverQueryProviderTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverQueryProviderTestsCoreOldest : MongoDBDriverQueryProviderTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverQueryProviderTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

}
