// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AwsSdk
{
    public abstract class InvokeLambdaTestBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private readonly string _function;
        private readonly string _qualifier;
        private readonly string _arn;
        private readonly bool _isAsync;

        public InvokeLambdaTestBase(TFixture fixture, ITestOutputHelper output, bool async, string function, string qualifier, string arn) : base(fixture)
        {
            _function = function;
            _qualifier = qualifier;
            _arn = arn;
            _isAsync = async;

            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));

            _fixture.TestLogger = output;
            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(20), 2);
                }
            );

            if (async)
            {
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaAsync {_function}:{_qualifier} \"fakepayload\"");
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaAsyncWithQualifier {_function} {_qualifier} \"fakepayload\"");
            }
            else
            {
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaSync {_function} \"fakepayload\"");
            }
            _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaSync fakefunction fakepayload");

            _fixture.Initialize();
        }

        [Fact]
        public void InvokeLambda()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
            };

            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactions = _fixture.AgentLog.GetTransactionEvents();

            Assert.NotNull(transactions);

            var spans = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("cloud.resource_id"))
                .ToList();

            Assert.Equal(_isAsync ? 2 : 1, spans.Count);

            foreach (var span in spans)
            {
                Assert.Equal(_arn, span.AgentAttributes["cloud.resource_id"]);
                Assert.Equal("aws_lambda", span.AgentAttributes["cloud.platform"]);
                Assert.Equal("InvokeRequest", span.AgentAttributes["aws.operation"]);
                Assert.Equal("us-west-2", span.AgentAttributes["aws.region"]);
            }
        }
    }
    [NetFrameworkTest]
    public class InvokeLambdaTest_Sync_FW462 : InvokeLambdaTestBase<ConsoleDynamicMethodFixtureFW462>
    {
        public InvokeLambdaTest_Sync_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output, false, "342444490463:NotARealFunction", null, "arn:aws:lambda:us-west-2:342444490463:function:NotARealFunction")
        {
        }
    }
    [NetFrameworkTest]
    public class InvokeLambdaTest_Sync_FWLatest : InvokeLambdaTestBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public InvokeLambdaTest_Sync_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output, false, "342444490463:NotARealFunction", null, "arn:aws:lambda:us-west-2:342444490463:function:NotARealFunction")
        {
        }
    }
    [NetCoreTest]
    public class InvokeLambdaTest_Async_CoreOldest : InvokeLambdaTestBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public InvokeLambdaTest_Async_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output, true, "342444490463:NotARealFunction", "NotARealAlias", "arn:aws:lambda:us-west-2:342444490463:function:NotARealFunction:NotARealAlias")
        {
        }
    }

    [NetCoreTest]
    public class InvokeLambdaTest_Async_CoreLatest : InvokeLambdaTestBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public InvokeLambdaTest_Async_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output, true, "342444490463:NotARealFunction", "NotARealAlias", "arn:aws:lambda:us-west-2:342444490463:function:NotARealFunction:NotARealAlias")
        {
        }
    }

}