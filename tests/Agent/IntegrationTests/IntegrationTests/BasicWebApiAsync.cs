// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class BasicWebApiAsync : IClassFixture<RemoteServiceFixtures.BasicWebApi>
    {
        private readonly RemoteServiceFixtures.BasicWebApi _fixture;

        public BasicWebApiAsync(RemoteServiceFixtures.BasicWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);

                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "requestParameters" }, "enabled", "true");

                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentationAsync.xml";

                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicWebApi", "BasicWebApi.Controllers.ValuesController", "AsyncMethod1", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "AsyncMethod1");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicWebApi", "BasicWebApi.Controllers.ValuesController", "AsyncMethod2", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "AsyncMethod2");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicWebApi", "BasicWebApi.Controllers.ValuesController", "ProcessResultAsync", "NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync", "ProcessResultAsync");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "BasicWebApi", "BasicWebApi.Controllers.ValuesController", "BackgroundThreadMethod", metricName: "BackgroundThreadMethod");
                },
                exerciseApplication: () =>
                {
                    _fixture.Async();

                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(2));
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/Microsoft.Owin.Host.HttpListener.OwinHttpListener/StartProcessingRequest", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/AsyncMethod1", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/AsyncMethod2", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/ProcessResultAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/BackgroundThreadMethod", callCount = 1},

                new Assertions.ExpectedMetric {metricName = @"DotNet/Microsoft.Owin.Host.HttpListener.OwinHttpListener/StartProcessingRequest", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/Values/Async", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/AsyncMethod1", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/AsyncMethod2", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/ProcessResultAsync", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"Custom/BackgroundThreadMethod", metricScope = "WebTransaction/WebAPI/Values/Async", callCount = 1}
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assert.Empty(_fixture.AgentLog.GetErrorTraces()),
                () => Assert.Empty(_fixture.AgentLog.GetErrorEvents())
            );

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault(sample => sample.Path == "WebTransaction/WebAPI/Values/Async");
            var expectedTransactionTraceSegments = new List<string>
            {
                @"DotNet/Microsoft.Owin.Host.HttpListener.OwinHttpListener/StartProcessingRequest",
                @"DotNet/Values/Async",
                @"AsyncMethod1",
                @"AsyncMethod2",
                @"ProcessResultAsync",
                @"BackgroundThreadMethod"
            };

            NrAssert.Multiple(
                () => Assert.NotNull(transactionSample),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
