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
    public class CouchbaseMiscTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseMiscTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_Unlock();
                    _fixture.Couchbase_Prepend();
                    _fixture.Couchbase_Append();
                    _fixture.Couchbase_Observe();
                    _fixture.Couchbase_Touch();
                    _fixture.Couchbase_Increment();
                    _fixture.Couchbase_Decrement();
                    _fixture.Couchbase_Exists();
                    _fixture.Couchbase_Invoke();
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/all", callCount = 9 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/allWeb", callCount = 9 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/all", callCount = 9 },
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allWeb", callCount = 9 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Unlock", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Unlock", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Unlock", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Unlock" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Prepend", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Prepend", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Prepend", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Prepend" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Append", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Append", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Append", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Append" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Observe", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Observe", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Observe", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Observe" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Touch", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Touch", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Touch", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Touch" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Increment", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Increment", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Increment", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Increment" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Decrement", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Decrement", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Decrement", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Decrement" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Exists", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Exists", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Exists", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Exists" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Invoke", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Invoke", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Invoke", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Invoke" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 9 },
            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = @"Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Unlock", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Unlock" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Prepend", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Prepend" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Append", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Append" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Observe", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Observe" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Touch", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Touch" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Increment", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Increment" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Decrement", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Decrement" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Exists", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Exists" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Invoke", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_Invoke" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Unlock"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Prepend"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Append"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Observe"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Touch"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Increment"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Decrement"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Exists"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Invoke"),
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
