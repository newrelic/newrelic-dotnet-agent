// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase3;

public abstract class Couchbase3TestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    protected readonly ConsoleDynamicMethodFixture _fixture;

    protected Couchbase3TestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.SetTimeout(TimeSpan.FromMinutes(2));

        _fixture.AddCommand("Couchbase3Exerciser Get");
        _fixture.AddCommand("Couchbase3Exerciser GetAndLockAndUnlock");
        _fixture.AddCommand("Couchbase3Exerciser Exists");
        _fixture.AddCommand("Couchbase3Exerciser InsertUpsertReplaceAndRemove");
        _fixture.AddCommand("Couchbase3Exerciser Mutate");
        _fixture.AddCommand("Couchbase3Exerciser Lookup");
        _fixture.AddCommand("Couchbase3Exerciser Scan");
        _fixture.AddCommand("Couchbase3Exerciser Touch");
        _fixture.AddCommand("Couchbase3Exerciser ScopeQuery");
        _fixture.AddCommand("Couchbase3Exerciser ClusterQuery");
        _fixture.AddCommand("Couchbase3Exerciser ScopeSearch");
        _fixture.AddCommand("Couchbase3Exerciser ClusterSearch");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.ConfigureFasterMetricsHarvestCycle(15);
                configModifier.ConfigureFasterErrorTracesHarvestCycle(15);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(15);

                configModifier.ForceTransactionTraces();
                configModifier.ForceSqlTraces();
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>()
        {
            new() { metricName = "Datastore/all", CallCountAllHarvests = 48},
            new() { metricName = "Datastore/allOther", CallCountAllHarvests = 48},
            new() { metricName = "Datastore/Couchbase/all", CallCountAllHarvests = 48},
            new() { metricName = "Datastore/Couchbase/allOther", CallCountAllHarvests = 48},

            new() { metricName = "Datastore/operation/Couchbase/ExistsAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAndLockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAndTouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/LookupInAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/LookupInAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/LookupInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/MutateInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/QueryAsync", CallCountAllHarvests = 15},
            new() { metricName = "Datastore/operation/Couchbase/RemoveAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/ScanAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/SearchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/SearchQueryAsync", CallCountAllHarvests = 12},
            new() { metricName = "Datastore/operation/Couchbase/TouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/TouchWithCasAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/UnlockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/UpsertAsync", CallCountAllHarvests = 1},

            new() { metricName = "Datastore/statement/Couchbase/hotel/MutateInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/QueryAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/SearchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/ExistsAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAndLockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAndTouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/users/LookupInAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/LookupInAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/LookupInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/RemoveAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/ScanAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/TouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/TouchWithCasAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/users/UnlockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/UpsertAsync", CallCountAllHarvests = 1},

        };

        var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
        {
            new Assertions.ExpectedSqlTrace
            {
                TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase.Couchbase3Exerciser/Get",
                Sql = "SELECT ?;",
                DatastoreMetricName = $"Datastore/operation/Couchbase/QueryAsync"
            }
        };


        var expectedTransactionEventIntrinsicAttributes = new List<string>
        {
            "databaseCallCount",
            "databaseDuration"
        };

        var expectedTransactionTraceSegmentParameters = new List<Assertions.ExpectedSegmentParameter>
        {
            new Assertions.ExpectedSegmentParameter { segmentName = $"Datastore/operation/Couchbase/QueryAsync", parameterName = "sql", parameterValue = "SELECT ?;"}
        };


        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();
        var transactionSample = _fixture.AgentLog.TryGetTransactionSample("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase.Couchbase3Exerciser/Get");

        Assert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
            () => Assert.All(transactionEvents, (t) => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, t)),
            () => Assertions.TransactionTraceSegmentParametersExist(expectedTransactionTraceSegmentParameters, transactionSample)
        );
    }

}

[NetCoreTest]
public class Couchbase3TestsCoreOldest : Couchbase3TestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public Couchbase3TestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[NetCoreTest]
public class Couchbase3TestsCoreLatest : Couchbase3TestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public Couchbase3TestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
