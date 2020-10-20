// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Couchbase;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Couchbase
{
    [NetFrameworkTest]
    public class CouchbaseInsertTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseInsertTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_Insert();
                    _fixture.Couchbase_InsertReplicatePersist();
                    _fixture.Couchbase_InsertWithExpiration();
                    _fixture.Couchbase_InsertReplicatePersistWithExpiration();
                    _fixture.Couchbase_InsertDocument();
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

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Insert", callCount = 5 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 5},
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Insert" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersist" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertReplicatePersistWithExpiration" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_InsertDocument" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 5 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Insert", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Insert" },
            };

            var insertTransactionEvent = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Insert");

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple
            (
                () => Assert.NotNull(insertTransactionEvent),

                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics)

            );
        }
    }
}
