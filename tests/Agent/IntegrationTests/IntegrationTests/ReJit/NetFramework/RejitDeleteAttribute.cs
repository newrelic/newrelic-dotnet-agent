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
    /// Tests deleting a single attribute (metricName) on a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.DeleteAttributeTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RejitDeleteAttribute : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitDeleteAttribute(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var deleteAttributeFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.DeleteAttributeTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(deleteAttributeFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperDeleteAttribute", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomDeleteMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestDeleteAttribute();
                    XmlUtils.DeleteXmlAttribute(deleteAttributeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory" }, "metricName");
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestDeleteAttribute();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetDeleteAttribute", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteAttribute", CallCountAllHarvests = 2 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", metricScope = "WebTransaction/Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteAttribute", metricScope = "WebTransaction/MVC/RejitController/GetDeleteAttribute", CallCountAllHarvests = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
