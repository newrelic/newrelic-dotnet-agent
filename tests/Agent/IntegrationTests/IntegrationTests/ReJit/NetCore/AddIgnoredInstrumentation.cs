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
    /// Tests that adding ignore settings for built-in and custom instrumentation cause the desired instrumentation
    /// to be ignored.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddNodeTest.xml
    /// </summary>
    [NetCoreTest]
    public class AddIgnoredInstrumentation : NewRelicIntegrationTest<AspNetCoreReJitMvcApplicationFixture>
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        public AddIgnoredInstrumentation(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    var document = CommonUtils.AddCustomInstrumentation(addNodeFilePath, "AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController", "CustomMethodDefaultWrapperAddNode", "NewRelic.Agent.Core.Wrapper.DefaultWrapper", "MyCustomAddMetricName", 7, false);
                    XmlUtils.AddXmlNode(addNodeFilePath, "urn:newrelic-extension", new[] { "extension", "instrumentation", "tracerFactory", "match" }, "exactMethodMatcher", string.Empty, "methodName", "CustomMethodDefaultWrapperAddNode1", false, document);
                    document.Save(addNodeFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.InitializeApp();

                    _fixture.TestAddNode(1);
                    _fixture.TestAddNode(0);

                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                    configModifier.AddIgnoredInstrumentationAssembly("Microsoft.AspNetCore.Mvc.Core");
                    configModifier.AddIgnoredInstrumentationAssemblyAndClass("AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController");

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ConfigFileChangeDetected, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.AgentConnectedLogLineRegex, Timing.TimeToConnect, 2);

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
                // From the initialize call
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/MVC/Home/Index", callCount = 1 },
                // From the first 2 calls to TestAddNode
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", callCount = 2 },
                // From the second set of calls after the mvc and custom instrumentations are disabled
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Rejit/GetAddNode/0", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Rejit/GetAddNode/1", callCount = 1 }
            };

            var metrics = CommonUtils.GetMetrics(_fixture.AgentLog);
            _fixture.TestLogger?.WriteLine(_fixture.AgentLog.GetFullLogAsString());

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
