// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class NetStandardLibraryInstrumentationFW : NetStandardLibraryInstrumentation<ConsoleDynamicMethodFixtureFWLatest>
    {
        public NetStandardLibraryInstrumentationFW(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NetStandardLibraryInstrumentationNetCoreOldest : NetStandardLibraryInstrumentation<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public NetStandardLibraryInstrumentationNetCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class NetStandardLibraryInstrumentationNetCoreLatest : NetStandardLibraryInstrumentation<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public NetStandardLibraryInstrumentationNetCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output) : base(fixture, output)
        {
        }
    }

    /// <summary>
    /// The goal of this test is to ensure that a .NET Standard library, which doesn't have a direct reference to
    /// MSCORLIB.dll is able to be instrumented.
    /// </summary>
    /// <typeparam name="TFixture"></typeparam>
    public abstract class NetStandardLibraryInstrumentation<TFixture>
        : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private TFixture _fixture;

        private const int COUNT_ITERATIONS = 10;

        public NetStandardLibraryInstrumentation(TFixture fixture, ITestOutputHelper output) : base(fixture)
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

                    var instrumentationXmlFilePath = Path.Combine(fixture.DestinationNewRelicExtensionsDirectoryPath, "TestCustomInstrumetnation.xml");

                    CommonUtils.AddCustomInstrumentation(
                        instrumentationFilePath: instrumentationXmlFilePath,
                        assemblyName: "NetStandardTestLibrary",
                        typeName: "NetStandardTestLibrary.NetStandardTestLibUtil",
                        methodName: "Test",
                        wrapperName: "NewRelic.Agent.Core.Tracer.Factories.BackgroundThreadTracerFactory");

                    _fixture.RemoteApplication.NewRelicConfig.SetLogLevel("finest");
                    _fixture.RemoteApplication.NewRelicConfig.ForceTransactionTraces();
                    _fixture.RemoteApplication.NewRelicConfig.SetOrDeleteDistributedTraceEnabled(true);
                    _fixture.RemoteApplication.NewRelicConfig.SetCATEnabled(false);

                    _fixture.AddCommand("AssemblyHelper LoadAssemblyFromFile NetStandardTestLibrary.dll");
                    _fixture.AddCommand($"NetStandardTestLibUtil Test {COUNT_ITERATIONS}");
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"OtherTransaction/Custom/NetStandardTestLibrary.NetStandardTestLibUtil/Test", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/NetStandardTestLibrary.NetStandardTestLibUtil/Test", callCount = COUNT_ITERATIONS + 1 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics().ToList();
            var actualTransactions = _fixture.AgentLog.GetTransactionEvents().ToList();
            var actualSpans = _fixture.AgentLog.GetSpanEvents().ToList();

            var transactionsDic = actualTransactions
                .Where(x => x.IntrinsicAttributes.ContainsKey("name"))
                .GroupBy(x => x.IntrinsicAttributes["name"].ToString())
                .ToDictionary(x => x.Key, x => x.Count());

            var spansDic = actualSpans
                .Where(x => x.IntrinsicAttributes.ContainsKey("name"))
                .GroupBy(x => x.IntrinsicAttributes["name"].ToString())
                .ToDictionary(x => x.Key, x => x.Count());

            NrAssert.Multiple
            (
                () => Assertions.MetricsExist(expectedMetrics, actualMetrics),
                () => Assertions.Equals(1, transactionsDic.Count()),
                () => Assertions.Equals(1, transactionsDic["OtherTransaction/Custom/NetStandardTestLibrary.NetStandardTestLibUtil/Test"]),
                () => Assertions.Equals(2, spansDic.Count()),
                () => Assertions.Equals(1, spansDic["OtherTransaction/Custom/NetStandardTestLibrary.NetStandardTestLibUtil/Test"]),
                () => Assertions.Equals(11, spansDic["DotNet/NetStandardTestLibrary.NetStandardTestLibUtil/Test"])
            );
        }
    }
}
