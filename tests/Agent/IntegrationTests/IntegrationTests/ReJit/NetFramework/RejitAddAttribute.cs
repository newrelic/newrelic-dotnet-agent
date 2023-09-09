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
    /// Tests adding an attribute (metricName) to a node (tracerFactory).
    /// Out of necessity, this attribute was removed prior to agent startup since it was easier to create then remove using existing methods.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddAttributeTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RejitAddAttribute : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitAddAttribute(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var addAttributeFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.AddAttributeTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(addAttributeFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddAttribute", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddBeforeMetricName", 7);
                    XmlUtils.DeleteXmlAttribute(addAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName");
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddAttribute();
                    XmlUtils.ModifyOrCreateXmlAttribute(addAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName", "MyCustomAddAfterMetricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestAddAttribute();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddAfterMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetAddAttribute", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddAfterMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddAttribute", CallCountAllHarvests = 2 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddAfterMetricName", metricScope = "WebTransaction/Custom/MyCustomAddAfterMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddAttribute", metricScope = "WebTransaction/MVC/RejitController/GetAddAttribute", CallCountAllHarvests = 1 }
            };

            var notExpcetedMetrics = new List<Assertions.ExpectedMetric>
            {
				//transactions
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddBeforeMetricName", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddBeforeMetricName", CallCountAllHarvests = 1 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddBeforeMetricName", metricScope = "WebTransaction/Custom/MyCustomAddBeforeMetricName", CallCountAllHarvests = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(notExpcetedMetrics, metrics)
            );
        }
    }
}
