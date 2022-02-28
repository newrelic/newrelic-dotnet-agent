// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging
{
    public abstract class SerilogHSMOrCSPDisablesForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public SerilogHSMOrCSPDisablesForwardingTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"LoggingTester SetFramework Serilog");
            _fixture.AddCommand($"LoggingTester Configure");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage One DEBUG");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Two INFO");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Three WARN");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage Four ERROR");
            _fixture.AddCommand($"LoggingTester CreateSingleLogMessage GetYourLogsOnTheDanceFloor FATAL");

            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier
                    .EnableApplicationLogging()
                    .EnableLogForwarding()
                    .EnableLogMetrics()
                    .EnableDistributedTrace()
                    .SetLogLevel("debug");

                    if (typeof(TFixture).ToString().Contains("HSM"))
                    {
                        // Set HSM to "true"
                        configModifier.SetHighSecurityMode(true);
                    }
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void NoLogDataIsSent()
        {
            var logData = _fixture.AgentLog.GetLogEventData().FirstOrDefault();
            Assert.Null(logData);

            // Making sure logging metrics aren't disabled
            var loggingMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = "Logging/lines", callCount = 5 },
            };

            var actualMetrics = _fixture.AgentLog.GetMetrics();
            Assertions.MetricsExist(loggingMetrics, actualMetrics);

        }
    }

    [NetFrameworkTest]
    public class SerilogHSMDisablesForwardingTestsFWLatestTests : SerilogHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public SerilogHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class SerilogCSPDisablesForwardingTestsFWLatestTests : SerilogHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public SerilogCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class SerilogHSMDisablesForwardingTestsNetCoreLatestTests : SerilogHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public SerilogHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    [NetCoreTest]
    public class SerilogCSPDisablesForwardingTestsNetCoreLatestTests : SerilogHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public SerilogCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
