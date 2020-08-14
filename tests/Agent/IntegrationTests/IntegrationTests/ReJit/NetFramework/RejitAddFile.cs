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
    /// Tests adding a new file containing a single node (tracerFactory).
    /// Out of necessity, this file is created outside the Extensions folder and then copied in later post agent startup.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddXmlFileTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RejitAddFile : IClassFixture<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitAddFile(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddFile();
                    var createFilePath = _fixture.RemoteApplication.DestinationNewRelicHomeDirectoryPath + @"\Integration.Testing.AddXmlFileTest.xml";
                    CommonUtils.AddCustomInstrumentation(createFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddFile", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddMetricName", 7);
                    var destinationFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.AddXmlFileTest.xml";
                    CommonUtils.MoveFile(createFilePath, destinationFilePath, TimeSpan.FromSeconds(5));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestAddFile();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetAddFile", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddFile", CallCountAllHarvests = 2 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomAddMetricName", metricScope = "WebTransaction/Custom/MyCustomAddMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetAddFile", metricScope = "WebTransaction/MVC/RejitController/GetAddFile", CallCountAllHarvests = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
