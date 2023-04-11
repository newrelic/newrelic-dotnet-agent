// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Elasticsearch
{
    public abstract class ElasticsearchSyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        protected enum ClientType
        {
            ElasticsearchNet,
            NEST,
            ElasticClients
        }

        protected readonly ConsoleDynamicMethodFixture _fixture;


        protected ElasticsearchSyncTestsBase(TFixture fixture, ITestOutputHelper output, string clientType) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            // TODO: Set high to allow for debugging
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));

            _fixture.AddCommand($"ElasticsearchExerciser SetClient {clientType}");

            _fixture.AddCommand($"ElasticsearchExerciser Index");

            _fixture.AddCommand($"ElasticsearchExerciser Search");

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
        public void IndexAndSearch()
        {
            var expectedTransactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch.ElasticsearchExerciser/Index";

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"Datastore/statement/Elasticsearch/flights/Index", metricScope = expectedTransactionName, callCount = 1 },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var traceId = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Equals(expectedTransactionName)).FirstOrDefault().IntrinsicAttributes["traceId"];

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["traceId"].ToString().Equals(traceId) && @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/statement/Elasticsearch"));


            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Equal(1, operationDatastoreSpans.Count())
            );
        }

    }

    #region NEST
    [NetFrameworkTest]
    public class ElasticsearchNestSyncTestsFWLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNestSyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestSyncTestsFW48 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNestSyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestSyncTestsFW471 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNestSyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestSyncTestsFW462 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNestSyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestSyncTestsCoreLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNestSyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestSyncTestsCore60 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNestSyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestSyncTestsCore50 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNestSyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestSyncTestsCore31 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNestSyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    #endregion NEST

    #region ElasticsearchNet
    [NetFrameworkTest]
    public class ElasticsearchNetSyncTestsFWLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNetSyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetSyncTestsFW48 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNetSyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetSyncTestsFW471 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNetSyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetSyncTestsFW462 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNetSyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetSyncTestsCoreLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNetSyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetSyncTestsCore60 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNetSyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetSyncTestsCore50 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNetSyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetSyncTestsCore31 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNetSyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }
    #endregion ElasticsearchNet

    #region ElasticClients
    [NetFrameworkTest]
    public class ElasticsearchElasticClientSyncTestsFWLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchElasticClientSyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientSyncTestsFW48 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchElasticClientSyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientSyncTestsFW471 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchElasticClientSyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientSyncTestsFW462 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchElasticClientSyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            // FW462 is testing MongoDB.Driver version 2.3, which needs to connect to the 3.2 server
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientSyncTestsCoreLatest : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchElasticClientSyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientSyncTestsCore60 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchElasticClientSyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientSyncTestsCore50 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchElasticClientSyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientSyncTestsCore31 : ElasticsearchSyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchElasticClientSyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }
    #endregion ElasticClients
}
