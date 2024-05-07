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
    public class CouchbaseGetTests : NewRelicIntegrationTest<RemoteServiceFixtures.CouchbaseBasicMvcFixture>
    {
        private readonly RemoteServiceFixtures.CouchbaseBasicMvcFixture _fixture;

        public CouchbaseGetTests(RemoteServiceFixtures.CouchbaseBasicMvcFixture fixture, ITestOutputHelper output)  : base(fixture)
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
                    _fixture.Couchbase_Get();
                    _fixture.Couchbase_GetMultiple();
                    _fixture.Couchbase_GetMultipleParallelOptions();
                    _fixture.Couchbase_GetMultipleParallelOptionsWithRangeSize();
                    _fixture.Couchbase_GetAndTouch();
                    _fixture.Couchbase_GetDocument();
                    _fixture.Couchbase_GetFromReplica();
                    _fixture.Couchbase_GetWithLock();

                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/all", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Datastore/allWeb", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/all", callCount = 7 },
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allWeb", callCount = 7 },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 4 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 4},
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouch" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetDocument" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplica" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLock" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/GetMultiple", callCount = 3 },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/GetMultiple", callCount = 3},
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultiple" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptions" },
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Couchbase/{CouchbaseTestObject.CouchbaseTestBucket}/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptionsWithRangeSize" },

				// We do not currently support datastore instance reporting for Couchbase
				new Assertions.ExpectedMetric { metricName = "Datastore/instance/Couchbase/unknown/unknown", callCount = 7 },

            };

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Datastore/allOther"},
                new Assertions.ExpectedMetric { metricName = "Datastore/Couchbase/allOther"},

				// The operation metric should not be scoped because the statement metric is scoped instead
				new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouch" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetDocument" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplica" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/Get", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLock" },

                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultiple" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptions" },
                new Assertions.ExpectedMetric { metricName = "Datastore/operation/Couchbase/GetMultiple", callCount = 1, metricScope = "WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptionsWithRangeSize" },
            };

            var transactionEvents = new List<TransactionEvent>
            {
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_Get"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetMultiple"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptions"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetMultipleParallelOptionsWithRangeSize"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetAndTouch"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetDocument"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetFromReplica"),
                _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/CouchbaseController/Couchbase_GetWithLock"),
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
