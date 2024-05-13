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
    public class CouchbaseRemoveTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseRemoveTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_RemoveCAS();
                    _fixture.Couchbase_RemoveReplicatePersist();
                    _fixture.Couchbase_RemoveCASReplicatePersist();
                    _fixture.Couchbase_RemoveMultiple();
                    _fixture.Couchbase_RemoveMultipleWithParallelOptions();
                    _fixture.Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize();
                    _fixture.Couchbase_RemoveDocument();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/all", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allWeb", callCount = 5 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Remove", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Remove", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Remove", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveReplicatePersist"},
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Remove", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveCASReplicatePersist"},

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/RemoveMultiple", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/RemoveMultiple", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultiple" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptions" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 5 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther" },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther" },

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Remove", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Remove", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveCASReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultiple" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptions" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/RemoveMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveCAS"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveReplicatePersist"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveCASReplicatePersist"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultiple"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptions"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveMultipleWithParallelOptionsWithRangeSize"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_RemoveDocument"),
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
