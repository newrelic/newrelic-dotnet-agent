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
    public abstract class MongoDBDriverIndexManagerTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private string _mongoUrl;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverIndexManagerTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            // Async methods first
            _fixture.AddCommand("MongoDBDriverExerciser CreateManyAsync");
            _fixture.AddCommand("MongoDBDriverExerciser CreateOneAsync");
            _fixture.AddCommand("MongoDBDriverExerciser DropAllAsync");
            _fixture.AddCommand("MongoDBDriverExerciser DropOneAsync");
            _fixture.AddCommand("MongoDBDriverExerciser ListAsync");
            // Then sync
            _fixture.AddCommand("MongoDBDriverExerciser CreateMany");
            _fixture.AddCommand("MongoDBDriverExerciser CreateOne");
            _fixture.AddCommand("MongoDBDriverExerciser DropAll");
            _fixture.AddCommand("MongoDBDriverExerciser DropOne");
            _fixture.AddCommand("MongoDBDriverExerciser List");

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

        [Theory]
        [InlineData("CreateOne")]
        [InlineData("CreateOneAsync")]
        [InlineData("CreateMany")]
        [InlineData("CreateManyAsync")]
        [InlineData("DropAll")]
        [InlineData("DropAllAsync")]
        [InlineData("DropOne")]
        [InlineData("DropOneAsync")]
        [InlineData("List")]
        [InlineData("ListAsync")]
        public void CheckForOperationMetrics(string operationName)
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/{operationName}");
            Assert.NotNull(m);
        }

    }

    [NetFrameworkTest]
    public class MongoDBDriverIndexManagerTestsFWLatest : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverIndexManagerTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverIndexManagerTestsFW48 : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverIndexManagerTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverIndexManagerTestsFW471 : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverIndexManagerTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverIndexManagerTestsFW462 : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverIndexManagerTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverIndexManagerTestsCoreLatest : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverIndexManagerTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverIndexManagerTestsCoreOldest : MongoDBDriverIndexManagerTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverIndexManagerTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

}
