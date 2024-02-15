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
    /// Tests that removing ignored built-in and custom instrumentation is restored correctly after the ignore settings
    /// are removed.
    /// Disables: Browser Monitoring
    /// Files: Integration.Testing.AddXmlFileTest.xml
    /// </summary>
    [NetFrameworkTest]
    public class RemoveIgnoredInstrumentation : NewRelicIntegrationTest<AspNetFrameworkReJitMvcApplicationFixture>
    {
        private readonly AspNetFrameworkReJitMvcApplicationFixture _fixture;

        public RemoveIgnoredInstrumentation(AspNetFrameworkReJitMvcApplicationFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;

            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AutoInstrumentBrowserMonitoring(false);
                    configModifier.AddIgnoredInstrumentationAssembly("System.Web.Mvc");
                    configModifier.AddIgnoredInstrumentationAssemblyAndClass("RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController");

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
                    configModifier.RemoveIgnoredInstrumentationAssembly("System.Web.Mvc");
                    configModifier.RemoveIgnoredInstrumentationAssemblyAndClass("RejitMvcApplication", "RejitMvcApplication.Controllers.RejitController");

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
                // From initialize and first call to TestAddFile while the ignore list is present
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/{controller}/{action}/{id}", callCount = 2 },
                // From the call after the ignore list removed
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", callCount = 1 },
                // Supportability metric indicating that the managed code successfully parsed the ignored instrumentation settings
                // This is only sent on the first metric harvest
                new Assertions.ExpectedMetric { metricName = @"Supportability/Dotnet/IgnoredInstrumentation", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
