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
    /// Tests that adding ignore settings for built-in and custom instrumentation cause the desired instrumentation
    /// to be ignored.
    /// Disables: Browser Monitoring
    /// Files: Integration.Testing.AddXmlFileTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class AddIgnoredInstrumentation : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public AddIgnoredInstrumentation(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);

                    var createFilePath = _fixture.RemoteApplication.DestinationNewRelicHomeDirectoryPath + @"\Integration.Testing.AddXmlFileTest.xml";
                    CommonUtils.AddCustomInstrumentation(createFilePath, "RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddFile", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddMetricName", 7);
                    var destinationFilePath = _fixture.RemoteApplication.DestinationExtensionsDirectoryPath + @"\Integration.Testing.AddXmlFileTest.xml";
                    CommonUtils.MoveFile(createFilePath, destinationFilePath, TimeSpan.FromSeconds(5));
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddFile();

                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AddIgnoredInstrumentationAssembly("System.Web.Mvc");
                    configModifier.AddIgnoredInstrumentationAssemblyAndClass("RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController");

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ConfigFileChangeDetected, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.AgentConnectedLogLineRegex, Timing.TimeToConnect, 2);

                    _fixture.TestAddFile();
                });

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                // From the initialize call
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/HomeController/Index", callCount = 1 },
                // From the first 2 calls to TestAddNode
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", callCount = 1 },
                // From the second set of calls after the mvc and custom instrumentations are disabled
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/{controller}/{action}/{id}", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
