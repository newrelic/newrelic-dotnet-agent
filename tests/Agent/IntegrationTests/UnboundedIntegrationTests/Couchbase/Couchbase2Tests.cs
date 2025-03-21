// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase;

public abstract class Couchbase2TestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    protected readonly ConsoleDynamicMethodFixture _fixture;

    protected Couchbase2TestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        string testDocumentId1 = Guid.NewGuid().ToString();
        string testDocumentId2 = Guid.NewGuid().ToString();
        var serializedTestUser = """
                                 {
                                   "name": "Keon Hoppe",
                                   "addresses": [
                                     {
                                       "type": "home",
                                       "address": "222 Sauer Neck",
                                       "city": "London",
                                       "country": "United Kingdom"
                                     },
                                     {
                                       "type": "work",
                                       "address": "6913 Rau Crossing",
                                       "city": "London",
                                       "country": "United Kingdom"
                                     }
                                   ],
                                   "driving_licence": "8d3931b5-51c5-58c8-9cf7-bc8ce9049558",
                                   "passport": "95bfb372-04e8-5865-9331-d3ec66ca631b",
                                   "preferred_email": "keonhoppe@vujojgo.nz",
                                   "preferred_phone": "(688) 606-2841",
                                   "preferred_airline": "inventory.airline.airline_2607",
                                   "preferred_airport": "inventory.airport.airport_507",
                                   "credit_cards": [
                                     {
                                       "type": "Mastercard",
                                       "number": "5161395257291763",
                                       "expiration": "2021-11"
                                     },
                                     {
                                       "type": "Visa",
                                       "number": "4986258227926866",
                                       "expiration": "2021-07"
                                     }
                                   ],
                                   "created": "2020-10-20",
                                   "updated": "2021-02-19"
                                 }
                                 """;

        _fixture.AddCommand($"Couchbase2Exerciser InsertTestDocument {testDocumentId1} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestUser))}"); // not in a transaction
        _fixture.AddCommand($"Couchbase2Exerciser InsertTestDocument {testDocumentId2} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestUser))}"); // not in a transaction


        _fixture.AddCommand($"Couchbase2Exerciser Exists {testDocumentId1}");
        _fixture.AddCommand($"Couchbase2Exerciser Get {testDocumentId1}");
        _fixture.AddCommand($"Couchbase2Exerciser GetMultiple {testDocumentId1},{testDocumentId2}");
        _fixture.AddCommand($"Couchbase2Exerciser GetAndLockAndUnlock {testDocumentId1}");
        _fixture.AddCommand($"Couchbase2Exerciser Lookup {testDocumentId1}");

        _fixture.AddCommand($"Couchbase2Exerciser RemoveTestDocument {testDocumentId1}"); // not in a transaction
        _fixture.AddCommand($"Couchbase2Exerciser RemoveTestDocument {testDocumentId2}"); // not in a transaction

        string insertUpsertReplaceDocumentId = Guid.NewGuid().ToString();
        var serializedUpsertTestUser = Newtonsoft.Json.JsonConvert.SerializeObject(new { Name = "Ted", Age = 35 });
        var serializedReplaceTestUser = Newtonsoft.Json.JsonConvert.SerializeObject(new { Name = "Bob", Age = 47 });
        _fixture.AddCommand($"Couchbase2Exerciser InsertUpsertReplaceAndRemove {insertUpsertReplaceDocumentId}, {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestUser))} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedUpsertTestUser))} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedReplaceTestUser))}");

        _fixture.AddCommand("Couchbase2Exerciser Touch"); // no params required
        _fixture.AddCommand("Couchbase2Exerciser BucketQuery"); // no params required
        _fixture.AddCommand("Couchbase2Exerciser BucketSearch"); // no params required

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;
                var configModifier = new NewRelicConfigModifier(configPath);

                configModifier.ForceTransactionTraces();
                configModifier.ForceSqlTraces();

                configModifier.SetLogLevel("finest");

                configModifier.ConfigureFasterMetricsHarvestCycle(30);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);
                configModifier.ConfigureFasterSqlTracesHarvestCycle(10);
                configModifier.ConfigureFasterTransactionTracesHarvestCycle(10);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2), 2);
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        int expectedCallCountAllHarvest = 0;

        if (_fixture is ConsoleDynamicMethodFixtureFW471)
            expectedCallCountAllHarvest = 21;
        else if (_fixture is ConsoleDynamicMethodFixtureFW48)
            expectedCallCountAllHarvest = 21;
        else if (_fixture is ConsoleDynamicMethodFixtureCoreOldest or ConsoleDynamicMethodFixtureCoreLatest)
            expectedCallCountAllHarvest = 31;

        Assert.True(expectedCallCountAllHarvest > 0, $"Unexpected test fixture TFM: {_fixture.GetType().Name}");

        var expectedMetrics = new List<Assertions.ExpectedMetric>()
        {
            new() { metricName = "Datastore/all", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/allOther", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/Couchbase/all", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/Couchbase/allOther", CallCountAllHarvests = expectedCallCountAllHarvest},

            new() { metricName = "Datastore/operation/Couchbase/ExistsAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAndLockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAndTouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/GetAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/LookupInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/MutateInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/RemoveAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/TouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/UnlockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/UpsertAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/QueryAsync", CallCountAllHarvests = 3 },
            new() { metricName = "Datastore/operation/Couchbase/AnalyticsQueryAsync", CallCountAllHarvests = 3}, // Scope.AnalyticsQueryAsync calls Cluster.AnalyticsQueryAsync

            new() { metricName = "Datastore/statement/Couchbase/hotel/MutateInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/ExistsAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAllReplicasAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAndLockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAndTouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAnyReplicaAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/GetAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/users/LookupInAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/RemoveAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/TouchAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/UnlockAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/users/UpsertAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/AnalyticsQueryAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/QueryAsync", CallCountAllHarvests = 1},
        };


        if (_fixture is not (ConsoleDynamicMethodFixtureFW48 or ConsoleDynamicMethodFixtureFW471))
        {
            expectedMetrics.AddRange(new List<Assertions.ExpectedMetric>
            {
                new() { metricName = "Datastore/operation/Couchbase/LookupInAllReplicasAsync", CallCountAllHarvests = 1 },
                new() { metricName = "Datastore/operation/Couchbase/LookupInAnyReplicaAsync", CallCountAllHarvests = 1 },
                new() { metricName = "Datastore/operation/Couchbase/ScanAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/operation/Couchbase/SearchAsync", CallCountAllHarvests = 4},
                new() { metricName = "Datastore/operation/Couchbase/SearchQueryAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/operation/Couchbase/TouchWithCasAsync", CallCountAllHarvests = 2},

                new() { metricName = "Datastore/statement/Couchbase/travel-sample/SearchAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/statement/Couchbase/users/LookupInAllReplicasAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/statement/Couchbase/users/LookupInAnyReplicaAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/statement/Couchbase/users/ScanAsync", CallCountAllHarvests = 1},
                new() { metricName = "Datastore/statement/Couchbase/users/TouchWithCasAsync", CallCountAllHarvests = 2},
            });
        }

        var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
        {
            new Assertions.ExpectedSqlTrace
            {
                TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase.Couchbase2Exerciser/ScopeAnalytics",
                Sql = "SELECT VALUE ap FROM airport_view ap limit ?;",
                DatastoreMetricName = $"Datastore/statement/Couchbase/travel-sample/AnalyticsQueryAsync"
            }
        };

        var expectedTransactionEventIntrinsicAttributes = new List<string>
        {
            "databaseCallCount",
            "databaseDuration"
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var transactionEvents = _fixture.AgentLog.GetTransactionEvents().ToList();
        var sqlTraces = _fixture.AgentLog.GetSqlTraces().ToList();

        Assert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.SqlTraceExists(expectedSqlTraces, sqlTraces),
            () => Assert.All(transactionEvents, (t) => Assertions.TransactionEventHasAttributes(expectedTransactionEventIntrinsicAttributes, TransactionEventAttributeType.Intrinsic, t))
        );
    }

}

[NetFrameworkTest]
public class Couchbase2TestsFW462 : Couchbase2TestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public Couchbase2TestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
