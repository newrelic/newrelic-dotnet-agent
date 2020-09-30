// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetCoreTest]
    public class NetCoreAttributeInstrumentationTests : IClassFixture<RemoteServiceFixtures.NetCoreAttributeInstrumentationFixture>
    {
        private readonly RemoteServiceFixtures.NetCoreAttributeInstrumentationFixture _fixture;

        public NetCoreAttributeInstrumentationTests(RemoteServiceFixtures.NetCoreAttributeInstrumentationFixture fixture, ITestOutputHelper output)
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

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", "NetCoreAttributeInstrumentationApplication.exe");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/NetCoreAttributeInstrumentationApplication.Program/DoSomething", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/NetCoreAttributeInstrumentationApplication.Program/DoSomething", metricScope = @"OtherTransaction/Custom/NetCoreAttributeInstrumentationApplication.Program/DoSomething", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/NetCoreAttributeInstrumentationApplication.Program/DoSomethingInside", metricScope = @"OtherTransaction/Custom/NetCoreAttributeInstrumentationApplication.Program/DoSomething", callCount = 1 },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/NetCoreAttributeInstrumentationApplication.Program/DoSomething",
                @"DotNet/NetCoreAttributeInstrumentationApplication.Program/DoSomethingInside"
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSamples = _fixture.AgentLog.GetTransactionSamples().ToList();

            var transactionSample = transactionSamples.Where(sample => sample.Path == @"OtherTransaction/Custom/NetCoreAttributeInstrumentationApplication.Program/DoSomething")
                .FirstOrDefault();

            Assert.NotNull(metrics);

            Assert.NotNull(transactionSample);

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
