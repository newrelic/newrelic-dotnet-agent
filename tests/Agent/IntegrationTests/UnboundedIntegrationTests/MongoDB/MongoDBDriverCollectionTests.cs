// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using System;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public abstract class MongoDBDriverCollectionTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;

        private MongoDBDriverVersion _driverVersion;

        private string _mongoUrl;

        private readonly string DatastorePath = "Datastore/statement/MongoDB/myCollection";

        public MongoDBDriverCollectionTestsBase(TFixture fixture, ITestOutputHelper output, string mongoUrl, MongoDBDriverVersion driverVersion) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _mongoUrl = mongoUrl;
            _driverVersion = driverVersion;

            _fixture.AddCommand($"MongoDbDriverExerciser SetMongoUrl {_mongoUrl}");
            // Async methods first
            _fixture.AddCommand("MongoDbDriverExerciser AggregateAsync");
            _fixture.AddCommand("MongoDbDriverExerciser BulkWriteAsync");
            _fixture.AddCommand("MongoDbDriverExerciser CountAsync");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser DistinctAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndDeleteAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndReplaceAsync");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndUpdateAsync");
            _fixture.AddCommand("MongoDbDriverExerciser InsertManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser InsertOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser MapReduceAsync");
            _fixture.AddCommand("MongoDbDriverExerciser ReplaceOneAsync");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateManyAsync");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateOneAsync");
             // Then sync methods
            _fixture.AddCommand("MongoDbDriverExerciser Aggregate");
            _fixture.AddCommand("MongoDbDriverExerciser BulkWrite");
            _fixture.AddCommand("MongoDbDriverExerciser Count");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteMany");
            _fixture.AddCommand("MongoDbDriverExerciser DeleteOne");
            _fixture.AddCommand("MongoDbDriverExerciser Distinct");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndDelete");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndReplace");
            _fixture.AddCommand("MongoDbDriverExerciser FindOneAndUpdate");
            _fixture.AddCommand("MongoDbDriverExerciser FindSync");
            _fixture.AddCommand("MongoDbDriverExerciser MapReduce");
            _fixture.AddCommand("MongoDbDriverExerciser InsertMany");
            _fixture.AddCommand("MongoDbDriverExerciser InsertOne");
            _fixture.AddCommand("MongoDbDriverExerciser ReplaceOne");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateMany");
            _fixture.AddCommand("MongoDbDriverExerciser UpdateOne");

            // the following commands are unavailable in MongoDB.Driver version 2.3
            if (_driverVersion > MongoDBDriverVersion.OldestSupportedOnFramework)
            {
                _fixture.AddCommand("MongoDbDriverExerciser CountDocumentsAsync");
                _fixture.AddCommand("MongoDbDriverExerciser EstimatedDocumentCountAsync");
                _fixture.AddCommand("MongoDbDriverExerciser WatchAsync");
                _fixture.AddCommand("MongoDbDriverExerciser CountDocuments");
                _fixture.AddCommand("MongoDbDriverExerciser EstimatedDocumentCount");
                _fixture.AddCommand("MongoDbDriverExerciser Watch");
            }

            // the following commands are unavailable in MongoDB.Driver versions <2.11
            if (_driverVersion >= MongoDBDriverVersion.AtLeast2_11)
            {
                _fixture.AddCommand("MongoDbDriverExerciser AggregateToCollectionAsync");
                _fixture.AddCommand("MongoDbDriverExerciser AggregateToCollection");
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
        [InlineData("Count", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("CountAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("Distinct", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("DistinctAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("MapReduce", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("MapReduceAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("InsertOne", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("InsertOneAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("InsertMany", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("InsertManyAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("ReplaceOne", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("ReplaceOneAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("UpdateOne", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("UpdateOneAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("UpdateMany", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("UpdateManyAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("DeleteOne", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("DeleteOneAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("DeleteMany", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("DeleteManyAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindSync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndDelete", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndDeleteAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndReplace", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndReplaceAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndUpdate", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("FindOneAndUpdateAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("BulkWrite", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("BulkWriteAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("Aggregate", MongoDBDriverVersion.OldestSupportedOnFramework)]
        [InlineData("AggregateAsync", MongoDBDriverVersion.OldestSupportedOnFramework)]

        // Methods unavailable in driver version 2.3
        [InlineData("CountDocuments", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("CountDocumentsAsync", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("EstimatedDocumentCount", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("EstimatedDocumentCountAsync", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("Watch", MongoDBDriverVersion.OldestSupportedOnCore)]
        [InlineData("WatchAsync", MongoDBDriverVersion.OldestSupportedOnCore)]

        //Methods availabile in driver versions >= 2.11
        [InlineData("AggregateToCollection", MongoDBDriverVersion.AtLeast2_11)]
        [InlineData("AggregateToCollectionAsync", MongoDBDriverVersion.AtLeast2_11)]

        public void CheckForMethodMetrics(string methodName, MongoDBDriverVersion minVersion)
        {
            if (_driverVersion >= minVersion)
            {
                var m = _fixture.AgentLog.GetMetricByName($"{DatastorePath}/{methodName}");
                Assert.True(m != null, $"Did not find metric for db operation named {methodName}");
            }
        }

    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFWLatest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public MongoDBDriverCollectionTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW48 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public MongoDBDriverCollectionTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW471 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public MongoDBDriverCollectionTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetFrameworkTest]
    public class MongoDBDriverCollectionTestsFW462 : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public MongoDBDriverCollectionTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            // 2.3 doesn't support the Watch/WatchAsync methods
            : base(fixture, output, MongoDbConfiguration.MongoDb3_2ConnectionString, MongoDBDriverVersion.OldestSupportedOnFramework)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverCollectionTestsCoreLatest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public MongoDBDriverCollectionTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.AtLeast2_11)
        {
        }
    }

    [NetCoreTest]
    public class MongoDBDriverCollectionTestsCoreOldest : MongoDBDriverCollectionTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public MongoDBDriverCollectionTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, MongoDbConfiguration.MongoDb6_0ConnectionString, MongoDBDriverVersion.OldestSupportedOnCore)
        {
        }
    }

    public enum MongoDBDriverVersion
    {
        OldestSupportedOnFramework, // 2.3
        OldestSupportedOnCore, // 2.8.1
        AtLeast2_11 // 2.11 or greater
    }

}
