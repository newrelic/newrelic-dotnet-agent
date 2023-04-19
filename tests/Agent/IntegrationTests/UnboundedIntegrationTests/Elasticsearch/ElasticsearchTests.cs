// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Elasticsearch
{
    public abstract class ElasticsearchTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        protected enum ClientType
        {
            ElasticsearchNet,
            NEST,
            ElasticClients
        }

        protected readonly ConsoleDynamicMethodFixture _fixture;

        protected readonly string _host = GetHostFromElasticServer(ElasticSearchConfiguration.ElasticServer);

        protected readonly ClientType _clientType;

        const string IndexName = "flights";


        protected ElasticsearchTestsBase(TFixture fixture, ITestOutputHelper output, ClientType clientType) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _clientType = clientType;

            // TODO: Set high to allow for debugging
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));

            _fixture.AddCommand($"ElasticsearchExerciser SetClient {clientType}");

            // Sync operations

            _fixture.AddCommand($"ElasticsearchExerciser Index");

            _fixture.AddCommand($"ElasticsearchExerciser Search");

            _fixture.AddCommand($"ElasticsearchExerciser IndexMany");

            _fixture.AddCommand($"ElasticsearchExerciser MultiSearch");

            // Async operations

            _fixture.AddCommand($"ElasticsearchExerciser IndexAsync");

            _fixture.AddCommand($"ElasticsearchExerciser SearchAsync");

            _fixture.AddCommand($"ElasticsearchExerciser IndexManyAsync");

            _fixture.AddCommand($"ElasticsearchExerciser MultiSearchAsync");

            // Don't like having to do this but it makes the async tests pass reliably
            _fixture.AddCommand("RootCommands DelaySeconds 5");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
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
            ValidateOperation("IndexAsync");
        }

        [Fact]
        public void MultiSearchAsync()
        {
            ValidateOperation("SearchAsync");
        }


        private void ValidateOperation(string operationName)
        {
            var expectedTransactionName = $"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch.ElasticsearchExerciser/{operationName}";

            var expectedIndexName = GetExpectedIndexName(operationName, _clientType);
            var expectedOperationName = GetExpectedOperationName(operationName, _clientType);

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Elasticsearch/{expectedIndexName}/{expectedOperationName}", metricScope = expectedTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var traceId = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals(expectedTransactionName)).FirstOrDefault().IntrinsicAttributes["traceId"];

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/statement/Elasticsearch"));

            var operationDatastoreAgentAttributes = operationDatastoreSpans.FirstOrDefault().AgentAttributes;

            var uri = operationDatastoreAgentAttributes.Where(x => x.Key == "peer.address").FirstOrDefault().Value;

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Single(operationDatastoreSpans),
                () => Assert.Equal(_host, uri)
            );
        }

        private static string GetHostFromElasticServer(string elasticServer)
        {
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

        private static string GetExpectedIndexName(string operationName, ClientType clientType)
        {
            if (operationName.StartsWith("IndexMany") || operationName.StartsWith("MultiSearch"))
            {
                return "Unknown";
            }
            else
            {
                return IndexName;
            }
        }
        private static string GetExpectedOperationName(string operationName, ClientType clientType)
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

    #region NEST
    [NetFrameworkTest]
    public class ElasticsearchNestTestsFWLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNestTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestTestsFW48 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNestTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestTestsFW471 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNestTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestTestsFW462 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNestTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCoreLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNestTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore60 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNestTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore50 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNestTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestTestsCore31 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNestTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST)
        {
        }
    }

    #endregion NEST

    #region ElasticsearchNet
    [NetFrameworkTest]
    public class ElasticsearchNetTestsFWLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNetTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetTestsFW48 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNetTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetTestsFW471 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNetTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetTestsFW462 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNetTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetTestsCoreLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNetTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetTestsCore60 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNetTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetTestsCore50 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNetTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetTestsCore31 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNetTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet)
        {
        }
    }
    #endregion ElasticsearchNet

    #region ElasticClients
    [NetFrameworkTest]
    public class ElasticsearchElasticClientTestsFWLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchElasticClientTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientTestsFW48 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchElasticClientTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientTestsFW471 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchElasticClientTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientTestsFW462 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchElasticClientTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientTestsCoreLatest : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchElasticClientTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientTestsCore60 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchElasticClientTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientTestsCore50 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchElasticClientTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientTestsCore31 : ElasticsearchTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchElasticClientTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients)
        {
        }
    }
    #endregion ElasticClients
}
