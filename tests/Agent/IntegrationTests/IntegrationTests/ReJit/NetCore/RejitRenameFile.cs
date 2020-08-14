// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.ReJit.NetCore
{
    /// <summary>
    /// Tests renaming file containing a single node (tracerFactory).
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.RenameOriginalXmlFileTest.xml, Integration.Testing.RenameTargetXmlFileTest.xml
    /// </summary>
    [NetCoreTest]
    public class RejitRenameFile : IClassFixture<AspNetCoreReJitMvcApplicationFixture>
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        private readonly string _renameOriginalFileFilePath;

        public RejitRenameFile(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;

            _renameOriginalFileFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.RenameOriginalXmlFileTest.xml";

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.SetLogLevel("finest");
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    CommonUtils.AddCustomInstrumentation(_renameOriginalFileFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperRenameFile", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomRenameMetricName", 7);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestRenameFile();
                    var renameTargetFileFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.RenameTargetXmlFileTest.xml";
                    CommonUtils.RenameFile(_renameOriginalFileFilePath, renameTargetFileFilePath, TimeSpan.FromSeconds(5));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.InstrumentationRefreshFileWatcherComplete, TimeSpan.FromMinutes(1));
                    _fixture.TestRenameFile();
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
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomRenameMetricName", callCount = 2 },

				// Unscoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenameMetricName", callCount = 2 },
                new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetRenameFile", callCount = 2},

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/HomeController/Index", metricScope = "WebTransaction/MVC/Home/Index", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/MyCustomRenameMetricName", metricScope = "WebTransaction/Custom/MyCustomRenameMetricName", callCount = 2 }
            };

            var notExpectedMetrics = new List<Assertions.ExpectedMetric>
            {
				//transactions
				new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/RejitController/GetRenameFile" },

				// Scoped
				new Assertions.ExpectedMetric { metricName = @"DotNet/RejitController/GetRenameFile", metricScope = "WebTransaction/MVC/RejitController/GetRenameFile" }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.MetricsDoNotExist(notExpectedMetrics, metrics)
            );
        }
    }

    public class RejitRenameFileWithTieredCompilation : RejitRenameFile, IClassFixture<AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation>
    {
        public RejitRenameFileWithTieredCompilation(AspNetCoreReJitMvcApplicationFixtureWithTieredCompilation fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
