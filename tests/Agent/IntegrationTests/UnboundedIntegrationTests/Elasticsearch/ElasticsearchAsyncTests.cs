// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.UnboundedIntegrationTests.ElasticsearchTests;
using NewRelic.Testing.Assertions;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.Elasticsearch
{
    public abstract class ElasticsearchAsyncTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        protected enum ClientType
        {
            ElasticsearchNet,
            NEST,
            ElasticClients
        }

        protected readonly ConsoleDynamicMethodFixture _fixture;

        protected ElasticsearchAsyncTestsBase(TFixture fixture, ITestOutputHelper output, string clientType) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            // TODO: Set high to allow for debugging
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));

            _fixture.AddCommand($"ElasticsearchExerciser SetClient {clientType}");

            _fixture.AddCommand($"ElasticsearchExerciser IndexAsync");

            _fixture.AddCommand($"ElasticsearchExerciser SearchAsync");

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
                new Assertions.ExpectedMetric { metricName = $"statement/Elasticsearch/flights/Index", metricScope = expectedTransactionName, callCount = 1 },
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
    public class ElasticsearchNestAsyncTestsFWLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNestAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestAsyncTestsFW48 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNestAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestAsyncTestsFW471 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNestAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNestAsyncTestsFW462 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNestAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestAsyncTestsCoreLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNestAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestAsyncTestsCore60 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNestAsyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestAsyncTestsCore50 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNestAsyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNestAsyncTestsCore31 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNestAsyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.NEST.ToString())
        {
        }
    }

    #endregion NEST

    #region ElasticsearchNet
    [NetFrameworkTest]
    public class ElasticsearchNetAsyncTestsFWLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchNetAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetAsyncTestsFW48 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchNetAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetAsyncTestsFW471 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchNetAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchNetAsyncTestsFW462 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchNetAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetAsyncTestsCoreLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchNetAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetAsyncTestsCore60 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchNetAsyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetAsyncTestsCore50 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchNetAsyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchNetAsyncTestsCore31 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchNetAsyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticsearchNet.ToString())
        {
        }
    }
    #endregion ElasticsearchNet

    #region ElasticClients
    [NetFrameworkTest]
    public class ElasticsearchElasticClientAsyncTestsFWLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ElasticsearchElasticClientAsyncTestsFWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientAsyncTestsFW48 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW48>
    {
        public ElasticsearchElasticClientAsyncTestsFW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientAsyncTestsFW471 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW471>
    {
        public ElasticsearchElasticClientAsyncTestsFW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetFrameworkTest]
    public class ElasticsearchElasticClientAsyncTestsFW462 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ElasticsearchElasticClientAsyncTestsFW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientAsyncTestsCoreLatest : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ElasticsearchElasticClientAsyncTestsCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientAsyncTestsCore60 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ElasticsearchElasticClientAsyncTestsCore60(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientAsyncTestsCore50 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore50>
    {
        public ElasticsearchElasticClientAsyncTestsCore50(ConsoleDynamicMethodFixtureCore50 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }

    [NetCoreTest]
    public class ElasticsearchElasticClientAsyncTestsCore31 : ElasticsearchAsyncTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public ElasticsearchElasticClientAsyncTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output, ClientType.ElasticClients.ToString())
        {
        }
    }
    #endregion ElasticClients
}
