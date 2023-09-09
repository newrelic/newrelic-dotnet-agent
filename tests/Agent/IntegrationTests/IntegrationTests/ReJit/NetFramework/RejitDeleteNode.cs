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
    /// Tests deleting a new node (tracerFactory) to an existing XML file with an existing (dummy) node as the second node.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.DeleteNodeTest.xml 
    /// </summary>
    [NetFrameworkTest]
    public class RejitDeleteNode : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitDeleteNode(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var deleteNodeFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.DeleteNodeTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    var document = CommonUtils.AddCustomInstrumentation(deleteNodeFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperDeleteNode", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomDeleteMetricName", 7, false);
                    XmlUtils.AddXmlNode(deleteNodeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory", "match" }, "exactMethodMatcher", string.Empty, "methodName", "CustomMethodDefaultWrapperDeleteNode1", true, document);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestDeleteNode(0);
                    _fixture.TestDeleteNode(1);
                    XmlUtils.DeleteXmlNode(deleteNodeFilePath, "urn:newrelic-extension",
                        new[] { "extension", "instrumentation", "tracerFactory", "match" }, "exactMethodMatcher"); // deletes first one (CustomMethodDefaultWrapperDeleteNode)
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestDeleteNode(0);
                    _fixture.TestDeleteNode(1);


                    // Uncommenting the following lines lead to odd metric counts. Manual testing of this scenario does not seem
                    // to indicate a problem. For now, just chalking it up to timing issue with how our tests are run...

                    //CommonUtils.DeleteXmlNode(_deleteNodeFilePath, "urn:newrelic-extension",
                    //	new[] { "extension", "instrumentation" }, "tracerFactory");
                    //_fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    //_fixture.TestDeleteNode(0);
                    //_fixture.TestDeleteNode(1);
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
				//transactions
				new Assertions.ExpectedMetric {metricName = @"WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 3},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/MVC/RejitController/GetDeleteNode", CallCountAllHarvests = 1},

				// Unscoped
				new Assertions.ExpectedMetric {metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/RejitController/GetDeleteNode", CallCountAllHarvests = 4},

				// Scoped
				new Assertions.ExpectedMetric {metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/MyCustomDeleteMetricName", metricScope = "WebTransaction/Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/RejitController/GetDeleteNode", metricScope = "WebTransaction/MVC/RejitController/GetDeleteNode", CallCountAllHarvests = 1}
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
