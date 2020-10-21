// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.CustomInstrumentation
{
    [NetFrameworkTest]
    public class AttributeInstrumentation : NewRelicIntegrationTest<RemoteServiceFixtures.AttributeInstrumentation>
    {
        private readonly RemoteServiceFixtures.AttributeInstrumentation _fixture;

        public AttributeInstrumentation(RemoteServiceFixtures.AttributeInstrumentation fixture, ITestOutputHelper output)
            : base(fixture)
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
                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/all", callCount = 4},

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

                new Assertions.ExpectedMetric {metricName = @"OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/AttributeInstrumentation.Program/MakeOtherTransactionWithCallToNetStandardMethod", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
                new Assertions.ExpectedMetric {metricName = @"DotNet/NetStandardClassLibrary.MyClass/MyMethodToBeInstrumented", metricScope = "OtherTransaction/Custom/AttributeInstrumentation.Program/MakeOtherTransactionWithCallToNetStandardMethod", callCount = 1},
            };

            var expectedTransactionEventAgentAttributes = new Dictionary<string, string>
            {
                { "request.uri", "/fizz/buzz" }
            };


            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            var transactionEvent = _fixture.AgentLog.GetTransactionEvents()
                .Where(e => e.IntrinsicAttributes["name"].ToString() == "WebTransaction/Uri/fizz/buzz")
                .FirstOrDefault();


            NrAssert.Multiple(
                () => Assertions.MetricsExist(expectedMetrics, metrics),
                () => Assertions.TransactionEventHasAttributes(expectedTransactionEventAgentAttributes, TransactionEventAttributeType.Agent, transactionEvent)
            );
        }
    }
}
