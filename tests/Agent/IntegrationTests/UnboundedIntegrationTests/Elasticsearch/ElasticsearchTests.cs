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

namespace NewRelic.Agent.UnboundedIntegrationTests.ElasticsearchTests
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

        protected ElasticsearchTestsBase(TFixture fixture, ITestOutputHelper output, string clientType, string syncMode) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            // TODO: Set high to allow for debugging
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));

            _fixture.AddCommand($"ElasticsearchExerciser SetClient {clientType}");

            _fixture.AddCommand($"ElasticsearchExerciser Index {syncMode}");

            _fixture.AddCommand($"ElasticsearchExerciser Search {syncMode}");

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

    }

}
