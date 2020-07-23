using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class BasicAspWebService : IClassFixture<RemoteServiceFixtures.BasicAspWebService>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicAspWebService _fixture;

        public BasicAspWebService([NotNull] RemoteServiceFixtures.BasicAspWebService fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.InvokeAsyncCall();
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"WebTransaction/ASP/testclient.aspx", callCount = 1},
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

            var expectedTransactionTraceSegments = new List<String>
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
                _fixture.AgentLog.GetTransactionSamples().Where(
                        sample => sample.Path == @"WebTransaction/WebService/HelloWorld/Greetings")
                    .FirstOrDefault();

            Assert.NotNull(transactionSample);
            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionTraceSegmentsExist(expectedTransactionTraceSegments, transactionSample)
            );
        }
    }
}
