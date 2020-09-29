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
    public class AspNetCoreCollectibleAssemblyContextTests : IClassFixture<RemoteServiceFixtures.AspNetCore3FeaturesFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCore3FeaturesFixture _fixture;

        private const int ExpectedTransactionCount = 2;

        public AspNetCoreCollectibleAssemblyContextTests(RemoteServiceFixtures.AspNetCore3FeaturesFixture fixture, ITestOutputHelper output)
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
            new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CurrentTransaction", callCount = ExpectedTransactionCount },
            new Assertions.ExpectedMetric { metricName = @"Supportability/ApiInvocation/CreateDistributedTracePayload", callCount = ExpectedTransactionCount },
        };

        private readonly List<Assertions.ExpectedMetric> _collectibleMetrics = new List<Assertions.ExpectedMetric>
        {
            new Assertions.ExpectedMetric { metricName = @"DotNet/CollectibleController/AccessCollectible", callCount = 2 },
            new Assertions.ExpectedMetric { metricName = @"DotNet/CollectibleController/AccessCollectible", metricScope = @"WebTransaction/MVC/Collectible/AccessCollectible", callCount = 2 },
        };
    }
}
