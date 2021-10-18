// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetCoreTest]
    public class NetCoreAttributeInstrumentationTestsCore31 : NetCoreAttributeInstrumentationTests<ConsoleDynamicMethodFixtureCore31>
    {
        public NetCoreAttributeInstrumentationTestsCore31(ConsoleDynamicMethodFixtureCore31 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public abstract class NetCoreAttributeInstrumentationTests<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private const string LibraryClassName = "MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.AttributeInstrumentation";

        protected readonly TFixture Fixture;

        public NetCoreAttributeInstrumentationTests(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            Fixture = fixture;
            Fixture.TestLogger = output;

            Fixture.AddCommand("AttributeInstrumentation DoSomething");

            Fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    //CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", "NetCoreAttributeInstrumentationApplication.exe");
                }
            );

            Fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = $"OtherTransaction/Custom/{LibraryClassName}/DoSomething", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/DoSomething", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/DoSomething", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = $"DotNet/{LibraryClassName}/DoSomethingInside", metricScope = $"OtherTransaction/Custom/{LibraryClassName}/DoSomething", callCount = 1 },
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                $"DotNet/{LibraryClassName}/DoSomething",
                $"DotNet/{LibraryClassName}/DoSomethingInside"
            };

            var metrics = Fixture.AgentLog.GetMetrics().ToList();
            var transactionSamples = Fixture.AgentLog.GetTransactionSamples().ToList();

            var transactionSample = transactionSamples.Where(sample => sample.Path == $"OtherTransaction/Custom/{LibraryClassName}/DoSomething")
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
