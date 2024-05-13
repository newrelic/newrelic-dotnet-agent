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
    public class CouchbaseAsyncUpsertTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseAsyncUpsertTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_UpsertAsync();
                    _fixture.Couchbase_UpsertCASReplicatePersistWithExpirationAsync();
                    _fixture.Couchbase_UpsertCASWithExpirationAsync();

                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/all", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allWeb", callCount = 3 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertAsync", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertAsync", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpirationAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpirationAsync" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 3 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertAsync"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpirationAsync"},
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/UpsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpirationAsync"},
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASReplicatePersistWithExpirationAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_UpsertCASWithExpirationAsync"),
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
