// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverAsyncCursorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverAsyncCursorTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddCommand("MongoDBDriverExerciser GetNextBatch");
            _fixture.AddCommand("MongoDBDriverExerciser GetNextBatchAsync");

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

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFWLatest : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverAsyncCursorTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFW471 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverAsyncCursorTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverAsyncCursorTestsFW462 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverAsyncCursorTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverAsyncCursorTestsCoreLatest : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverAsyncCursorTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverAsyncCursorTestsCore50 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public MongoDBDriverAsyncCursorTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverAsyncCursorTestsCore31 : MongoDBDriverAsyncCursorTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public MongoDBDriverAsyncCursorTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
