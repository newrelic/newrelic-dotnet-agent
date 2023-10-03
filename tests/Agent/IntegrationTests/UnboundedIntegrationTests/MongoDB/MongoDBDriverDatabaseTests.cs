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
    public abstract class MongoDBDriverDatabaseTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;
        private string _mongoUrl;
        private MongoDBDriverVersion _driverVersion;

        private readonly string DatastoreStatementPathBase = "Datastore/statement/MongoDB";
        private readonly string DatastoreOperationPathBase = "Datastore/operation/MongoDB";

        public MongoDBDriverDatabaseTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl, MongoDBDriverVersion driverVersion)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;
            _driverVersion = driverVersion;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
             // Async methods first
            _fixture.AddCommand("MongoDBDriverExerciser CreateCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser DropCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser ListCollectionsAsync");
            _fixture.AddCommand("MongoDBDriverExerciser RenameCollectionAsync");
            _fixture.AddCommand("MongoDBDriverExerciser RunCommandAsync");
            // Then sync methods
            _fixture.AddCommand("MongoDBDriverExerciser CreateCollection");
            _fixture.AddCommand("MongoDBDriverExerciser DropCollection");
            _fixture.AddCommand("MongoDBDriverExerciser ListCollections");
            _fixture.AddCommand("MongoDBDriverExerciser RenameCollection");
            _fixture.AddCommand("MongoDBDriverExerciser RunCommand");

            if (_driverVersion > MongoDBDriverVersion.OldestSupportedOnFramework)
            {
                _fixture.AddCommand("MongoDBDriverExerciser ListCollectionNamesAsync");
                _fixture.AddCommand("MongoDBDriverExerciser WatchDBAsync");
                _fixture.AddCommand("MongoDBDriverExerciser ListCollectionNames");
                _fixture.AddCommand("MongoDBDriverExerciser WatchDB");
            }

            if (_driverVersion > MongoDBDriverVersion.OldestSupportedOnCore)
            {
                _fixture.AddCommand("MongoDBDriverExerciser AggregateDBAsync");
                _fixture.AddCommand("MongoDBDriverExerciser AggregateDBToCollectionAsync");
                _fixture.AddCommand("MongoDBDriverExerciser AggregateDB");
                _fixture.AddCommand("MongoDBDriverExerciser AggregateDBToCollection");
            }

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
        [InlineData("createTestCollection", "CreateCollection")]
        [InlineData("createTestCollectionAsync", "CreateCollectionAsync")]
        [InlineData("dropTestCollection", "DropCollection")]
        [InlineData("dropTestCollectionAsync", "DropCollectionAsync")]
        public void CheckForStatementMetrics(string collectionName, string operationName)
        {
            var m = _fixture.AgentLog.GetMetricByName($"{DatastoreStatementPathBase}/{collectionName}/{operationName}");
            Assert.NotNull(m);
        }

        [Theory]
        [InlineData("ListCollections", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("ListCollectionsAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("RenameCollection", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("RenameCollectionAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("RunCommand", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("RunCommandAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        // Methods not available in driver version 2.3
        [InlineData("ListCollectionNames", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("ListCollectionNamesAsync", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("Watch", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("WatchAsync", MongoDBDriverVersion.OldestSupportedOnCore)]
        // Methods not available in driver version 2.8
        [InlineData("Aggregate", MongoDBDriverVersion.AtLeast2_11)]
        [InlineData("AggregateAsync", MongoDBDriverVersion.AtLeast2_11)]
        [InlineData("AggregateToCollection", MongoDBDriverVersion.AtLeast2_11)]
        [InlineData("AggregateToCollectionAsync", MongoDBDriverVersion.AtLeast2_11)]
        public void CheckForOperationMetrics(string operationName, MongoDBDriverVersion minVersion)
        {
            if (_driverVersion >= minVersion)
            {
                var m = _fixture.AgentLog.GetMetricByName($"{DatastoreOperationPathBase}/{operationName}");
                Assert.NotNull(m);
            }
        }

    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFWLatest : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverDatabaseTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFW48 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverDatabaseTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFW471 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverDatabaseTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverDatabaseTestsFW462 : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverDatabaseTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString, MongoDBDriverVersion.OldestSupportedOnFramework)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverDatabaseTestsCoreLatest : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverDatabaseTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverDatabaseTestsCoreOldest : MongoDBDriverDatabaseTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverDatabaseTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.OldestSupportedOnCore)
        {
        }
    }

}
