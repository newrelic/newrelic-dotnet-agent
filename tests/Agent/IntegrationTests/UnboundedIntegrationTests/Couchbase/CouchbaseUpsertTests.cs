// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase
{
    [NetFrameworkTest]
    public class CouchbaseUpsertTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseUpsertTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                },
                exerciseApplication: () =>
                {
                    _fixture.Couchbase_Upsert();
                    _fixture.Couchbase_UpsertCASWithExpiration();
                    _fixture.Couchbase_UpsertReplicatePersist();
                    _fixture.Couchbase_UpsertReplicatePersistWithExpiration();
                    _fixture.Couchbase_UpsertCASReplicatePersistWithExpiration();
                    _fixture.Couchbase_UpsertMultiple();
                    _fixture.Couchbase_UpsertMultipleParallelOptions();
                    _fixture.Couchbase_UpsertMultipleParallelOptionsWithRangeSize();
                    _fixture.Couchbase_UpsertDocument();

                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/all", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allWeb", callCount = 8 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersistWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertDocument" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertMultiple", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertMultiple", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultiple" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptions" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptionsWithRangeSize" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 8 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpiration"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersist"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersistWithExpiration"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpiration"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Upsert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertDocument"},

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultiple"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptions"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptionsWithRangeSize"},
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersist"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertReplicatePersistWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultiple"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptions"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertMultipleParallelOptionsWithRangeSize"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertDocument"),
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            transactionEvents.ForEach(Assert.NotNull);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)

            );
        }
    }
}
