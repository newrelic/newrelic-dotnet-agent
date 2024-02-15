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
    /// Tests that removing ignored built-in and custom instrumentation is restored correctly after the ignore settings
    /// are removed.
    /// Disables: Browser Monitoring
    /// Logging: finest
    /// Files: Integration.Testing.AddNodeTest.xml
    /// </summary>
    [NetCoreTest]
    public class RemoveIgnoredInstrumentation : NewRelicIntegrationTest<AspNetCoreReJitMvcApplicationFixture>
    {
        private readonly AspNetCoreReJitMvcApplicationFixture _fixture;

        public RemoveIgnoredInstrumentation(AspNetCoreReJitMvcApplicationFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    configModifier.AddIgnoredInstrumentationAssembly("Microsoft.AspNetCore.Mvc.Core");
                    configModifier.AddIgnoredInstrumentationAssemblyAndClass("AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController");

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
                    configModifier.RemoveIgnoredInstrumentationAssembly("Microsoft.AspNetCore.Mvc.Core");
                    configModifier.RemoveIgnoredInstrumentationAssemblyAndClass("AspNetCoreMvcRejitApplication", "RejitMvcApplication.Controllers.RejitController");

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
                // From the initialize call with MVC instrumentation ignored
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/ROOT", callCount = 1 },
                // From the first set of calls when the mvc and custom instrumentations are disabled
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Rejit/GetAddNode/0", callCount = 1 },
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/ASP/Rejit/GetAddNode/1", callCount = 1 },
                // From the second 2 calls to TestAddNode after the instrumentations are enabled
                new Assertions.ExpectedMetric { metricName = @"WebTransaction/Custom/MyCustomAddMetricName", callCount = 2 },
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
