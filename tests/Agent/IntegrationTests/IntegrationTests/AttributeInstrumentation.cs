using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class AttributeInstrumentation : IClassFixture<RemoteServiceFixtures.AttributeInstrumentation>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.AttributeInstrumentation _fixture;

        public AttributeInstrumentation([NotNull] RemoteServiceFixtures.AttributeInstrumentation fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                },
                exerciseApplication: () =>
                {
                    // Nothing to do. Test app does it all.
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"WebTransaction", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/all", callCount = 3},

                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWork", callCount = 3},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWorkAsync", callCount = 2},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeMoreWorkAsync", callCount = 2},

                new Assertions.ExpectedMetric {metricName = @"WebTransaction/Custom/AttributeInstrumentation.Program/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeWebTransaction", metricScope = "WebTransaction/Custom/AttributeInstrumentation.Program/MakeWebTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWork", metricScope = "WebTransaction/Custom/AttributeInstrumentation.Program/MakeWebTransaction", callCount = 1},

                new Assertions.ExpectedMetric {metricName = @"WebTransaction/Uri/fizz/buzz", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeWebTransactionWithCustomUri", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeWebTransactionWithCustomUri", metricScope = "WebTransaction/Uri/fizz/buzz", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWork", metricScope = "WebTransaction/Uri/fizz/buzz", callCount = 1},

                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransaction", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransaction", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWork", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransaction", callCount = 1},

                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWorkAsync", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeMoreWorkAsync", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionThenCallAsyncMethod", callCount = 1},

                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionAsync", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeWorkAsync", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionAsync", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/DoSomeMoreWorkAsync", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionAsync", callCount = 1},
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics)
            );
        }
    }
}
