// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.ReJit.NetFramework
{
    /// <summary>
    /// Tests deleting file containing a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.DeleteXmlFileTest.xml
    /// </summary>
    public class RejitDeleteFile : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RejitDeleteFile(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            var deleteFileFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.DeleteXmlFileTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(deleteFileFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperDeleteFile", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomDeleteMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestDeleteFile();
                    CommonUtils.DeleteFile(deleteFileFilePath, TimeSpan.FromSeconds(5));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestDeleteFile();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetDeleteFile", CallCountAllHarvests = 1 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteFile", CallCountAllHarvests = 2 },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/HomeController/Index", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomDeleteMetricName", metricScope = "WebTransaction/Custom/MyCustomDeleteMetricName", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetDeleteFile", metricScope = "WebTransaction/MVC/RejitController/GetDeleteFile", CallCountAllHarvests = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
