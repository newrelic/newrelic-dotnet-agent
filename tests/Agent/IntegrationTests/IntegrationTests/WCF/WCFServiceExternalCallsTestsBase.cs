// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.WCF.Service
{
    [NetFrameworkTest]
    public abstract class WCFServiceExternalCallsTestsBase : WCFEmptyTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public WCFServiceExternalCallsTestsBase(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output, HostingModel hostingModelOption)
            : base(fixture, output, hostingModelOption, WCFBindingType.BasicHttp) { }

        protected override void SetupConfiguration()
        {
            base.SetupConfiguration();

            _fixture.RemoteApplication.NewRelicConfig.SetRequestTimeout(TimeSpan.FromSeconds(10));
        }

        protected override void AddFixtureCommands()
        {
            base.AddFixtureCommands();

            _fixture.AddCommand($"WCFClient TellWCFServerToMakeExternalCalls");
        }

        [Fact]
        protected void ExternalCallsTests()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"WebTransactionTotalTime/WCF/NewRelic.Agent.IntegrationTests.Shared.Wcf.IWcfService.TAPMakeExternalCalls" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/google.com/Stream/GET" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/bing.com/Stream/GET" },
                new Assertions.ExpectedMetric(){ callCount =1, metricName = $"External/yahoo.com/Stream/GET" }
            };

            Assertions.MetricsExist(expectedMetrics, _logHelpers.MetricValues);
        }
    }
}
#endif
