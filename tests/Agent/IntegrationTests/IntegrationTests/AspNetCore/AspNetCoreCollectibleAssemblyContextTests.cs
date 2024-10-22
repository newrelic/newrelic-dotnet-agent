// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AspNetCore
{
    [NetCoreTest]
    public class AspNetCoreCollectibleAssemblyContextTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreFeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreFeaturesFixture _fixture;

        private const int ExpectedTransactionCount = 2;

        public AspNetCoreCollectibleAssemblyContextTests(RemoteServiceFixtures.AspNetCoreFeaturesFixture fixture, ITestOutputHelper output)
            : base(fixture)
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
                    configModifier.SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AccessCollectible();
                    _fixture.AccessCollectible();
                    _fixture.TestLogger?.WriteLine(_fixture.ProfilerLog.GetFullLogAsString());
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assert.NotNull(metrics);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(_generalMetrics, metrics),
                () => Assertions.MetricsExist(_collectibleMetrics, metrics)
            );

            var expectedTransactionTraceSegments = new List<string>
            {
                @"Middleware Pipeline",
                @"DotNet/CollectibleController/AccessCollectible"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/MVC/Collectible/AccessCollectible");

            Assert.NotNull(transactionSample);
            Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample);
        }

        private readonly List<Assertions.ExpectedMetric> _generalMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CurrentTransaction", CallCountAllHarvests = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/InsertDistributedTraceHeaders", CallCountAllHarvests = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _collectibleMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"DotNet/CollectibleController/AccessCollectible", CallCountAllHarvests = 2 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/CollectibleController/AccessCollectible", metricScope = @"WebTransaction/MVC/Collectible/AccessCollectible", CallCountAllHarvests = 2 },
        };
    }
}
