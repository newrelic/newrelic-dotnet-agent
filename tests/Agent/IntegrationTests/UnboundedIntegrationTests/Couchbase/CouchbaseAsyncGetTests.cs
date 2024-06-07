// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase
{
    [NetFrameworkTest]
    public class CouchbaseAsyncGetTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseAsyncGetTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_GetAsync();
                    _fixture.Couchbase_GetAndTouchAsync();
                    _fixture.Couchbase_GetDocumentAsync();
                    _fixture.Couchbase_GetFromReplicaAsync();
                    _fixture.Couchbase_GetWithLockAsync();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "Datastore/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allWeb", callCount = 5 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/Couchbase/Get", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouchAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetDocumentAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplicaAsync" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLockAsync" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 5 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouchAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetDocumentAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplicaAsync" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLockAsync" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouchAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetDocumentAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplicaAsync"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLockAsync"),
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
