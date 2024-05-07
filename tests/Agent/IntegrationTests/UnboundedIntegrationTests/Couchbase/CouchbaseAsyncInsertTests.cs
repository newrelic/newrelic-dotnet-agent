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
    public class CouchbaseAsyncInsertTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseAsyncInsertTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_InsertAsync();
                    _fixture.Couchbase_InsertReplicatePersistAsync();
                    _fixture.Couchbase_InsertWithExpirationAsync();
                    _fixture.Couchbase_InsertReplicatePersistWithExpirationAsync();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/all", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "Datastore/allWeb", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/all", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allWeb", callCount = 4 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/InsertAsync", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/InsertAsync", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertWithExpirationAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistWithExpirationAsync" },
				

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 4 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allOther"},
				
				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertWithExpirationAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/InsertAsync", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistWithExpirationAsync" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_InsertAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_InsertWithExpirationAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistWithExpirationAsync"),
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
