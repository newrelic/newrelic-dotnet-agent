// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation
{
    [NetFrameworkTest]
    public class BasicAspWebService : IClassFixture<RemoteServiceFixtures.BasicAspWebService>
    {

        private readonly RemoteServiceFixtures.BasicAspWebService _fixture;

        public BasicAspWebService(RemoteServiceFixtures.BasicAspWebService fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var newRelicConfig = _fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfig);
                    configModifier.ForceTransactionTraces();

                    //The purpose of this custom instrumentation file is to ignore noisy transactions.
                    var instrumentationFilePath = $@"{fixture.DestinationNewRelicExtensionsDirectoryPath}\CustomInstrumentation.xml";
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "System.Web.Extensions", "System.Web.Handlers.ScriptResourceHandler", "ProcessRequest", "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "System.Web", "System.Web.Handlers.AssemblyResourceLoader", "GetAssemblyInfo", "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory");
                    CommonUtils.AddCustomInstrumentation(instrumentationFilePath, "System.Web.Extensions", "System.Web.Script.Services.RestClientProxyHandler", "ProcessRequest", "NewRelic.Agent.Core.Tracer.Factories.IgnoreTransactionTracerFactory");
                },
                exerciseApplication: () =>
                {
                    _fixture.InvokeAsyncCall();
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.TransactionSampleLogLineRegex);
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/System.Web.Script.Services.WebServiceMethodData/CallMethod", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings" ,callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AuthenticateRequest", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AuthorizeRequest", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/ResolveRequestCache", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/MapRequestHandler", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AcquireRequestState", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/ExecuteRequestHandler", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/ReleaseRequestState", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/UpdateRequestCache", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/EndRequest", metricScope = @"WebTransaction/WebService/HelloWorld/Greetings", callCount = 1},
            };

            var expectedTransactionTraceSegments = new List<string>
            {
                @"AuthenticateRequest",
                @"AuthorizeRequest",
                @"ResolveRequestCache",
                @"MapRequestHandler",
                @"AcquireRequestState",
                @"ExecuteRequestHandler",
                @"ReleaseRequestState",
                @"UpdateRequestCache",
                @"EndRequest",
            };
            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionSample =
                _fixture.AgentLog.GetTransactionSamples()
                .FirstOrDefault(sample => sample.Path == @"WebTransaction/WebService/HelloWorld/Greetings");

            Assert.NotNull(transactionSample);
            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
