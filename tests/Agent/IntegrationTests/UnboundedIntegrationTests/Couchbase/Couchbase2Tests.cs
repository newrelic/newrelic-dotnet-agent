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
        var serializedTestAirline = """{"id":10,"type":"airline","name":"40-Mile Air","iata":"Q5","icao":"MLA","callsign":"MILE-AIR","country":"United States"}""";

        _fixture.AddCommand($"Couchbase2Exerciser InsertTestDocument {testDocumentId1} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestAirline))}"); // not in a transaction
        _fixture.AddCommand($"Couchbase2Exerciser InsertTestDocument {testDocumentId2} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestAirline))}"); // not in a transaction

        _fixture.AddCommand($"Couchbase2Exerciser Exists {testDocumentId1}");
        _fixture.AddCommand($"Couchbase2Exerciser Get {testDocumentId1}");
        _fixture.AddCommand($"Couchbase2Exerciser GetMultiple {testDocumentId1},{testDocumentId2}");
        _fixture.AddCommand($"Couchbase2Exerciser GetAndLockAndUnlock {testDocumentId1}");

        _fixture.AddCommand($"Couchbase2Exerciser RemoveTestDocument {testDocumentId1}"); // not in a transaction
        _fixture.AddCommand($"Couchbase2Exerciser RemoveTestDocument {testDocumentId2}"); // not in a transaction

        string insertUpsertReplaceDocumentId = Guid.NewGuid().ToString();
        var serializedUpsertTestAirline =
            """{ "id":10748,"type":"airline","name":"Locair","iata":"ZQ","icao":"LOC","callsign":"LOCAIR","country":"United States"}""";
        var serializedReplaceTestAirline =
                """{"id":10748,"type":"airline","name":"Locair","iata":"ZQ","icao":"LOC","callsign":"LOCAIR","country":"United States"}""";
        _fixture.AddCommand($"Couchbase2Exerciser InsertUpsertReplaceAndRemove {insertUpsertReplaceDocumentId} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedTestAirline))} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedUpsertTestAirline))} {Convert.ToBase64String(Encoding.UTF8.GetBytes(serializedReplaceTestAirline))}");

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
        int expectedCallCountAllHarvest = 26;

        var expectedMetrics = new List<Assertions.ExpectedMetric>()
        {
            new() { metricName = "Datastore/all", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/allOther", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/Couchbase/all", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/Couchbase/allOther", CallCountAllHarvests = expectedCallCountAllHarvest},
            new() { metricName = "Datastore/instance/Couchbase/unknown/unknown", CallCountAllHarvests = expectedCallCountAllHarvest},

            new() { metricName = "Datastore/operation/Couchbase/ExistsAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/Get", CallCountAllHarvests = 9},
            new() { metricName = "Datastore/operation/Couchbase/GetMultiple", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/UnlockAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/UpsertAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/operation/Couchbase/RemoveAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/TouchAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/operation/Couchbase/Query", CallCountAllHarvests = 1 },
            new() { metricName = "Datastore/operation/Couchbase/QueryAsync", CallCountAllHarvests = 3 },

            new() { metricName = "Datastore/statement/Couchbase/travel-sample/ExistsAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/Get", CallCountAllHarvests = 9},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/GetMultiple", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/UnlockAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/InsertAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/UpsertAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/ReplaceAsync", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/RemoveAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/TouchAsync", CallCountAllHarvests = 2},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/Query", CallCountAllHarvests = 1},
            new() { metricName = "Datastore/statement/Couchbase/travel-sample/QueryAsync", CallCountAllHarvests = 3},
        };

        var expectedSqlTraces = new List<Assertions.ExpectedSqlTrace>
        {
            new Assertions.ExpectedSqlTrace
            {
                TransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Couchbase.Couchbase2Exerciser/BucketQuery",
                Sql = "SELECT t.* FROM ? t LIMIT ?",
                DatastoreMetricName = $"Datastore/statement/Couchbase/travel-sample/Query"
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
