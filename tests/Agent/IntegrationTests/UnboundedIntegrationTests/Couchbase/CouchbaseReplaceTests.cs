// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
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
    public class CouchbaseReplaceTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseReplaceTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_Replace();
                    _fixture.Couchbase_ReplaceCAS();
                    _fixture.Couchbase_ReplaceWithExpiration();
                    _fixture.Couchbase_ReplaceCASWithExpiration();
                    _fixture.Couchbase_ReplaceReplicatePersist();
                    _fixture.Couchbase_ReplaceCASReplicatePersist();
                    _fixture.Couchbase_ReplaceCASReplicatePersistWithExpiration();
                    _fixture.Couchbase_ReplaceDocument();
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

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 8 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Replace" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCAS" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersistWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceDocument" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 8 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Replace" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCAS" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceWithExpiration" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASWithExpiration" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersistWithExpiration" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Replace", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceDocument" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Replace"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCAS"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceReplicatePersist"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersist"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceCASReplicatePersistWithExpiration"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_ReplaceDocument"),
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            transactionEvents.ForEach(Assert.NotNull);

            NrAssert.Multiple(transactionEvents.Select<TransactionEvent, Action>(x => () => Assert.NotNull(x)).ToArray());

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)
            );
        }
    }
}
