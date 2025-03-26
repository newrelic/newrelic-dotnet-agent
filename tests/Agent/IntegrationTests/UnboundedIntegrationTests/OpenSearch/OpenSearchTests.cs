// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.UnboundedIntegrationTests.OpenSearch
{
    public abstract class OpenSearchTestsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        protected readonly ConsoleDynamicMethodFixture _fixture;

        protected readonly string _host;

        const string IndexName = "flights";

        protected OpenSearchTestsTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _host = GetHostFromElasticServer();

            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            // Required to set up the connection to the OpenSearch server
            _fixture.AddCommand($"OpenSearchExerciser ConnectAsync");

            // Async operations
            _fixture.AddCommand($"OpenSearchExerciser IndexAsync");
            _fixture.AddCommand($"OpenSearchExerciser SearchAsync");
            _fixture.AddCommand($"OpenSearchExerciser IndexManyAsync");
            _fixture.AddCommand($"OpenSearchExerciser MultiSearchAsync");
            _fixture.AddCommand($"OpenSearchExerciser GenerateErrorAsync");

            // Sync operations
            _fixture.AddCommand($"OpenSearchExerciser Index");
            _fixture.AddCommand($"OpenSearchExerciser Search");
            _fixture.AddCommand($"OpenSearchExerciser IndexMany");
            _fixture.AddCommand($"OpenSearchExerciser MultiSearch");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ConfigureFasterMetricsHarvestCycle(15);
                    configModifier.ConfigureFasterErrorTracesHarvestCycle(15);
                    configModifier.ConfigureFasterSpanEventsHarvestCycle(15);

                    configModifier.ForceTransactionTraces();
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Index()
        {
            ValidateOperation("Index");
        }

        [Fact]
        public void Search()
        {
            ValidateOperation("Search");
        }

        [Fact]
        public void IndexMany()
        {
            ValidateOperation("IndexMany");
        }

        [Fact]
        public void MultiSearch()
        {
            ValidateOperation("MultiSearch");
        }

        [Fact]
        public void IndexAsync()
        {
            ValidateOperation("IndexAsync");
        }

        [Fact]
        public void SearchAsync()
        {
            ValidateOperation("SearchAsync");
        }

        [Fact]
        public void IndexManyAsync()
        {
            ValidateOperation("IndexManyAsync");
        }

        [Fact]
        public void MultiSearchAsync()
        {
            ValidateOperation("MultiSearchAsync");
        }

        [Fact]
        public void ErrorAsync()
        {
            ValidateError("GenerateErrorAsync");
        }

        private void ValidateError(string operationName)
        {
            var errorTrace =
                _fixture.AgentLog.TryGetErrorTrace(
                    $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.OpenSearch.OpenSearchExerciser/{operationName}");
            Assert.NotNull(errorTrace);
        }


        private void ValidateOperation(string operationName)
        {
            var expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.OpenSearch.OpenSearchExerciser/{operationName}";

            var expectedIndexName = IndexName;
            var expectedOperationName = GetExpectedOperationName(operationName);

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/OpenSearch/{expectedIndexName}/{expectedOperationName}", metricScope = expectedTransactionName, callCount = 1 },
            };
            var expectedAgentAttributes = new List<string>
            {
                "db.system",
                "db.operation",
                "db.instance",
                "peer.address",
                "peer.hostname",
                "server.address",
                "server.port"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var traceId = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals(expectedTransactionName)).FirstOrDefault().IntrinsicAttributes["traceId"];

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/statement/OpenSearch"));

            var operationDatastoreAgentAttributes = operationDatastoreSpans.FirstOrDefault()?.AgentAttributes;

            var uri = operationDatastoreAgentAttributes?.Where(x => x.Key == "peer.address").FirstOrDefault().Value;

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Single(operationDatastoreSpans),
                () => Assert.Equal(_host, uri),
                () => Assertions.SpanEventHasAttributes(expectedAgentAttributes, SpanEventAttributeType.Agent, operationDatastoreSpans.FirstOrDefault())
            );
        }

        // Using the Elasticsearch server credentials since it OpenSearch supports it.
        private static string GetHostFromElasticServer()
        {
            var elasticServer = ElasticSearchConfiguration.ElasticServer;

            if (elasticServer.StartsWith("https://"))
            {
                return elasticServer.Remove(0, "https://".Length);
            }
            else if (elasticServer.StartsWith("http://"))
            {
                return elasticServer.Remove(0, "http://".Length);
            }
            else
            {
                return string.Empty;
            }
        }

        private static string GetExpectedOperationName(string operationName)
        {
            if (operationName.StartsWith("IndexMany"))
            {
                return "Bulk";
            }
            else
            {
                return operationName.Replace("Async", "");
            }
        }
    }

    #region OpenSearchClient
    [NetFrameworkTest]
    public class OpenSearchClientTestsFWLatest : OpenSearchTestsTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public OpenSearchClientTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class OpenSearchClientTestsFW462 : OpenSearchTestsTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public OpenSearchClientTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class OpenSearchClientTestsCoreLatest : OpenSearchTestsTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public OpenSearchClientTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class OpenSearchClientTestsCoreOldest : OpenSearchTestsTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public OpenSearchClientTestsCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    #endregion Clients
}
