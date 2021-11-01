// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.UnboundedIntegrationTests.CosmosDB
{
    public abstract class CosmosDBTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly ConsoleDynamicMethodFixture _fixture;


        protected CosmosDBTestsBase(TFixture fixture, ITestOutputHelper output)  : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

            _fixture.AddCommand($"CosmosDBExerciser StartAgent");
            _fixture.AddCommand($"CosmosDBExerciser CreateReadAndDeleteDatabase test_db_{Guid.NewGuid():x}");


            _fixture.AddCommand($"CosmosDBExerciser Wait");


            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier.ForceTransactionTraces();
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
                new Assertions.ExpectedMetric { metricName = "Datastore/CosmosDB/all", callCount = 7 },

                new Assertions.ExpectedMetric { metricName = "Datastore/instance/CosmosDB/localhost/8081", callCount = 7 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadFeedDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateCollection", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/CreateDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 1 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/ReadDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 2 },

                new Assertions.ExpectedMetric { metricName = $"Datastore/operation/CosmosDB/DeleteDatabase", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.CosmosDB.CosmosDBExerciser/CreateReadAndDeleteDatabase", callCount = 1 },

            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var spanEvents = _fixture.AgentLog.GetSpanEvents();

            var operationDatastoreSpans = spanEvents.Where(@event => @event.IntrinsicAttributes["name"].ToString().Contains("Datastore/operation/CosmosDB"));


            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Equal(6, operationDatastoreSpans.Count())
            );
        }
    }


    [NetFrameworkTest]
    public class CosmosDBTestsFW : CosmosDBTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public CosmosDBTestsFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }


    [NetCoreTest]
    public class CosmosDBTestsCore : CosmosDBTestsBase<ConsoleDynamicMethodFixtureCore31>
    {
        public CosmosDBTestsCore(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
