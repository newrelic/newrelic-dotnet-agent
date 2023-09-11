// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.ReJit.NetFramework
{
    /// <summary>
    /// Tests changing the value for a single attribute (metricName) on a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.ChangeAttributeTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RejitChangeAttributeValue : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitChangeAttributeValue(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var changeAttributeFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.ChangeAttributeTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(changeAttributeFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperChangeAttributeValue", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomChangeMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestChangeAttributeValue();
                    XmlUtils.ModifyOrCreateXmlAttribute(changeAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName", "MyCustomRenamedMetricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestChangeAttributeValue();
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				//transactions
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomChangeMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomRenamedMetricName", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomChangeMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenamedMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetChangeAttributeValue", CallCountAllHarvests = 2 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomChangeMetricName", metricScope = "WebTransaction/Custom/MyCustomChangeMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenamedMetricName", metricScope = "WebTransaction/Custom/MyCustomRenamedMetricName", CallCountAllHarvests = 1 }
            };

            var notExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				//transactions
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetChangeAttributeValue" },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetChangeAttributeValue", metricScope = "WebTransaction/MVC/RejitController/GetChangeAttributeValue" }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(notExpectedMetrics, metrics)
            );
        }
    }
}
