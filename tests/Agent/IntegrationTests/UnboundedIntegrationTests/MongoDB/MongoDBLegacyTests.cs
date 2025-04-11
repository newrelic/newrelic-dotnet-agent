// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.MongoDB
{
    public class MongoDBLegacyTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureFW462>
    {
        private const string CollectionName = "myCollection";
        private const string StatementRoot = "Datastore/statement/MongoDB/" + CollectionName;
        private const string OperationRoot = "Datastore/operation/MongoDB";
        private const string TransactionRoot = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.MongoDB.MongoDBLegacyExerciser";

        private readonly ConsoleDynamicMethodFixture _fixture;

        public MongoDBLegacyTests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddCommand($"MongoDBLegacyExerciser SetupClient");
            _fixture.AddCommand($"MongoDBLegacyExerciser Insert");
            _fixture.AddCommand($"MongoDBLegacyExerciser Find");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindOne");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindOneById");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindOneAs");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindAll");
            _fixture.AddCommand($"MongoDBLegacyExerciser GenericFind");
            _fixture.AddCommand($"MongoDBLegacyExerciser GenericFindAs");
            _fixture.AddCommand($"MongoDBLegacyExerciser CursorGetEnumerator");
            _fixture.AddCommand($"MongoDBLegacyExerciser OrderedBulkInsert");
            _fixture.AddCommand($"MongoDBLegacyExerciser UnorderedBulkInsert");
            _fixture.AddCommand($"MongoDBLegacyExerciser Update");
            _fixture.AddCommand($"MongoDBLegacyExerciser Remove");
            _fixture.AddCommand($"MongoDBLegacyExerciser RemoveAll");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindAndModify");
            _fixture.AddCommand($"MongoDBLegacyExerciser FindAndRemove");
            _fixture.AddCommand($"MongoDBLegacyExerciser CreateIndex");
            _fixture.AddCommand($"MongoDBLegacyExerciser GetIndexes");
            _fixture.AddCommand($"MongoDBLegacyExerciser IndexExistsByName");
            _fixture.AddCommand($"MongoDBLegacyExerciser Aggregate");
            _fixture.AddCommand($"MongoDBLegacyExerciser Validate");
            _fixture.AddCommand($"MongoDBLegacyExerciser ParallelScanAs");
            _fixture.AddCommand($"MongoDBLegacyExerciser Drop");
            _fixture.AddCommand($"MongoDBLegacyExerciser CleanupClient");

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
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedDatastoreCallCount = 55;

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $@"Datastore/all", callCount = expectedDatastoreCallCount },
                new Assertions.ExpectedMetric { metricName = $@"Datastore/allOther", callCount = expectedDatastoreCallCount },

                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/CreateCollection", metricScope = $"{TransactionRoot}/SetupClient" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Insert", metricScope = $"{TransactionRoot}/Insert" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Find", metricScope = $"{TransactionRoot}/Find" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindOne", metricScope = $"{TransactionRoot}/FindOne" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindOne", metricScope = $"{TransactionRoot}/FindOneById" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindOne", metricScope = $"{TransactionRoot}/FindOneAs" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindAll", metricScope = $"{TransactionRoot}/FindAll" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Find", metricScope = $"{TransactionRoot}/GenericFind" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Find", metricScope = $"{TransactionRoot}/GenericFindAs" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/GetEnumerator", metricScope = $"{TransactionRoot}/CursorGetEnumerator" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/InitializeOrderedBulkOperation", metricScope = $"{TransactionRoot}/OrderedBulkInsert" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/InitializeUnorderedBulkOperation", metricScope = $"{TransactionRoot}/UnorderedBulkInsert" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Update", metricScope = $"{TransactionRoot}/Update" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Remove", metricScope = $"{TransactionRoot}/Remove" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/RemoveAll", metricScope = $"{TransactionRoot}/RemoveAll" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindAndModify", metricScope = $"{TransactionRoot}/FindAndModify" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/FindAndRemove", metricScope = $"{TransactionRoot}/FindAndRemove" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/CreateIndex", metricScope = $"{TransactionRoot}/CreateIndex" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/GetIndexes", metricScope = $"{TransactionRoot}/GetIndexes" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/IndexExistsByName", metricScope = $"{TransactionRoot}/IndexExistsByName" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Aggregate", metricScope = $"{TransactionRoot}/Aggregate" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Validate", metricScope = $"{TransactionRoot}/Validate" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/ParallelScanAs", metricScope = $"{TransactionRoot}/ParallelScanAs" },
                new Assertions.ExpectedMetric { metricName = $@"{StatementRoot}/Drop", metricScope = $"{TransactionRoot}/Drop" },

                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/CreateCollection" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Insert" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Find" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/FindOne" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/GetEnumerator" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/FindAll" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/InitializeOrderedBulkOperation" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/BulkWriteOperation Insert" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/BulkWriteOperation Execute" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/InitializeUnorderedBulkOperation" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Update" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Remove" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/RemoveAll" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/FindAndModify" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/FindAndRemove" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/CreateIndex" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/GetIndexes" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/IndexExistsByName" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Aggregate" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Validate" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/ParallelScanAs" },
                new Assertions.ExpectedMetric { metricName = $@"{OperationRoot}/Drop" },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
