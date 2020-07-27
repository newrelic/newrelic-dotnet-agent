using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class HttpClientInstrumentationDisablesForLegacyPipeline : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

        public HttpClientInstrumentationDisablesForLegacyPipeline([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var webConfigPath = Path.Combine(fixture.DestinationApplicationDirectoryPath, "web.config");
                    new WebConfigModifier(webConfigPath).ForceLegacyAspPipeline();

                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.GetHttpClient();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"External/all" },
                new Assertions.ExpectedMetric { metricName = @"External/allWeb" },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/all" },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/all" },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/all" },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET" },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET" },
                new Assertions.ExpectedMetric { metricName = @"External/www.bing.com/Stream/GET" },
                new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/GET", metricScope = @"WebTransaction/MVC/DefaultController/HttpClient" },
                new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/GET", metricScope = @"WebTransaction/MVC/DefaultController/HttpClient" }
                
                // There should NOT be a "bing" external metric scoped to the transaction because the embedded task.run is not a supported use case
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionEventWithExternal = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes.ContainsKey("externalDuration"))
                .FirstOrDefault();

            var transactionEventForController = _fixture.AgentLog.TryGetTransactionEvent("WebTransaction/MVC/DefaultController/HttpClient");

            NrAssert.Multiple
            (
                () => Assertions.MetricsDoNotExist(unexpectedMetrics, metrics),
                () => Assert.Null(transactionEventWithExternal),
                () => Assert.NotNull(transactionEventForController)
            );

            var httpClientSuppressedRegex =
                @".* The method (.+) in class (.+) from assembly (.+) will not be instrumented. (.*)";
            Assert.NotNull(_fixture.AgentLog.TryGetLogLine(httpClientSuppressedRegex));
        }
    }
}
