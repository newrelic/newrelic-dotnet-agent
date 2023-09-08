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
    public abstract class MongoDBDriverAsyncCursorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private string _mongoUrl;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverAsyncCursorTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            _fixture.AddCommand("MongoDBDriverExerciser GetNextBatch");
            _fixture.AddCommand("MongoDBDriverExerciser GetNextBatchAsync");

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
            _mongoUrl = mongoUrl;
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

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFWLatest : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverAsyncCursorTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFW48 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverAsyncCursorTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFW471 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverAsyncCursorTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFW462 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverAsyncCursorTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverAsyncCursorTestsCoreLatest : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverAsyncCursorTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverAsyncCursorTestsCoreOldest : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverAsyncCursorTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString)
        {
        }
    }

}
