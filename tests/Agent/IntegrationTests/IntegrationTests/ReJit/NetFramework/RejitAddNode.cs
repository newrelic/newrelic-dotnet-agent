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
    /// Tests adding two new nodes (tracerFactory and additional exactMethodMatcher) to an existing XML file with no nodes.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddNodeTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RejitAddNode : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitAddNode(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var addNodeFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.AddNodeTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.CreateEmptyInstrumentationFile(addNodeFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddNode(1); // this will cause this method require a full rejit (differnet profiler code)
                    var document = CommonUtils.AddCustomInstrumentation(addNodeFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddNode", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddMetricName", 7, false);
                    XmlUtils.AddXmlNode(addNodeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory", "match" }, "exactMethodMatcher", string.Empty, "methodName", "CustomMethodDefaultWrapperAddNode1", false, document);
                    // Potential future addition: Adding a test for new match element.  not adding now since it would require a second app to test really well.
                    document.Save(addNodeFilePath);
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestAddNode(0);
                    _fixture.TestAddNode(1);
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetAddNode", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddNode", CallCountAllHarvests = 3 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", metricScope = "WebTransaction/Custom/MyCustomAddMetricName", CallCountAllHarvests = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddNode", metricScope = "WebTransaction/MVC/RejitController/GetAddNode", CallCountAllHarvests = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
