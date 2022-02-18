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
    public abstract class Log4netHSMOrCSPDisablesForwardingTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public Log4netHSMOrCSPDisablesForwardingTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(System.TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"Log4netTester Configure");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage One DEBUG");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Two INFO");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Three WARN");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage Four ERROR");
            _fixture.AddCommand($"Log4netTester CreateSingleLogMessage GetYourLogsOnTheDanceFloor FATAL");

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
    public class Log4netHSMDisablesForwardingTestsFWLatestTests : Log4netHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestHSM>
    {
        public Log4netHSMDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class Log4netCSPDisablesForwardingTestsFWLatestTests : Log4netHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureFWLatestCSP>
    {
        public Log4netCSPDisablesForwardingTestsFWLatestTests(ConsoleDynamicMethodFixtureFWLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class Log4netHSMDisablesForwardingTestsNetCoreLatestTests : Log4netHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestHSM>
    {
        public Log4netHSMDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestHSM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
    [NetCoreTest]
    public class Log4netCSPDisablesForwardingTestsNetCoreLatestTests : Log4netHSMOrCSPDisablesForwardingTestsBase<ConsoleDynamicMethodFixtureCoreLatestCSP>
    {
        public Log4netCSPDisablesForwardingTestsNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatestCSP fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
