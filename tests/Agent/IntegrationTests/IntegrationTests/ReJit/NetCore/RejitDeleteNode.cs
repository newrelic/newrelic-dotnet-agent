// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.ReJit.NetCore
{
    /// <summary>
    /// Tests deleting a new node (tracerFactory) to an existing XML file with an existing (dummy) node as the second node.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.DeleteNodeTest.xml 
    /// </summary>
    [NetCoreTest]
    public abstract class RejitDeleteNodeBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AspNetCoreReJitMvcApplicationFixture
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        protected RejitDeleteNodeBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            var deleteNodeFilePath = Path.Combine(_fixture.RemoteApplication.DestinationExtensionsDirectoryPath, "Integration.Testing.DeleteNodeTest.xml");

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    var document = CommonUtils.AddCustomInstrumentation(deleteNodeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperDeleteNode", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomDeleteMetricName", 7, false);
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
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/MVC/Home/Index", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/Custom/MyCustomDeleteMetricName", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/MVC/Rejit/GetDeleteNode/{id}", callCount = 1},

                // Unscoped
                new Assertions.ExpectedMetric {metricName = @"DotNet/HomeController/Index", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/MyCustomDeleteMetricName", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/RejitController/GetDeleteNode", callCount = 4},

                // Scoped
                new Assertions.ExpectedMetric {metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/MyCustomDeleteMetricName", metricScope = "WebTransaction/Custom/MyCustomDeleteMetricName", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/RejitController/GetDeleteNode", metricScope = "WebTransaction/MVC/Rejit/GetDeleteNode/{id}", callCount = 1}
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }

    public class RejitDeleteNode : RejitDeleteNodeBase<AspNetCoreReJitMvcApplicationFixture>
    {
        public RejitDeleteNode(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class RejitDeleteNodeWithTieredCompilation : RejitDeleteNodeBase<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitDeleteNodeWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
