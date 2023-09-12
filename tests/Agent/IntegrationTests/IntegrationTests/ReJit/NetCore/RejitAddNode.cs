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
    /// Tests adding two new nodes (tracerFactory and additional exactMethodMatcher) to an existing XML file with no nodes.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddNodeTest.xml
    /// </summary>
    [NetCoreTest]
    public abstract class RejitAddNodeBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture: AspNetCoreReJitMvcApplicationFixture
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        protected RejitAddNodeBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;

            var addNodeFilePath = Path.Combine(_fixture.RemoteApplication.DestinationExtensionsDirectoryPath, "Integration.Testing.AddNodeTest.xml");

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.CreateEmptyInstrumentationFile(addNodeFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddNode(1); // this will cause this method require a full rejit (differnet profiler code)
                    var document = CommonUtils.AddCustomInstrumentation(addNodeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddNode", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddMetricName", 7, false);
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Rejit/GetAddNode/{id}", callCount = 1 },

                // Unscoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddNode", callCount = 3 },

                // Scoped
                new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", metricScope = "WebTransaction/Custom/MyCustomAddMetricName", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddNode", metricScope = "WebTransaction/MVC/Rejit/GetAddNode/{id}", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }

    public class RejitAddNode : RejitAddNodeBase<AspNetCoreReJitMvcApplicationFixture>
    {
        public RejitAddNode(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }

    }

    public class RejitAddNodeWithTieredCompilation : RejitAddNodeBase<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitAddNodeWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
