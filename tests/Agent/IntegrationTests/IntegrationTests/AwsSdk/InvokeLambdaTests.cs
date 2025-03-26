// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;


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

        public InvokeLambdaTestBase(TFixture fixture, ITestOutputHelper output, bool useAsync, string function, string qualifier, string arn) : base(fixture)
        {
            _function = function;
            _qualifier = qualifier;
            _arn = arn;
            _isAsync = useAsync;

            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));

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
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2),2);
                }
            );

            if (useAsync)
            {
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaAsync {_function}:{_qualifier} \"fakepayload\"");
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaAsyncWithQualifier {_function} {_qualifier} \"fakepayload\"");
            }
            else
            {
                _fixture.AddCommand($"InvokeLambdaExerciser InvokeLambdaSync {_function} \"fakepayload\"");
            }
            _fixture.Initialize();
        }

        [Fact]
        public void InvokeLambda()
        {
            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var expectedCount = _isAsync ? 2 : 1;
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric {metricName = @"DotNet/InvokeRequest", CallCountAllHarvests = expectedCount},
            };
            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactions = _fixture.AgentLog.GetTransactionEvents().ToList();
            Assert.Equal(expectedCount, transactions.Count());

            foreach (var transaction in transactions)
            {
                Assert.StartsWith("OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.AwsSdk.InvokeLambdaExerciser/InvokeLambda", transaction.IntrinsicAttributes["name"].ToString());
            }

            var allSpans = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("cloud.platform"))
                .ToList();
            Assert.Equal(expectedCount, allSpans.Count);

            foreach (var span in allSpans)
            {
                Assert.Equal("aws_lambda", span.AgentAttributes["cloud.platform"]);
                Assert.Equal("InvokeRequest", span.AgentAttributes["aws.operation"]);
                Assert.Equal("us-west-2", span.AgentAttributes["aws.region"]);
            }

            // There should be one fewer span in this list, because there's one where there wasn't
            // enough info to create an ARN
            var spansWithArn = _fixture.AgentLog.GetSpanEvents()
                .Where(e => e.AgentAttributes.ContainsKey("cloud.resource_id"))
                .ToList();
            Assert.Equal(expectedCount, spansWithArn.Count);
            foreach (var span in spansWithArn)
            {
                Assert.Equal(_arn, span.AgentAttributes["cloud.resource_id"]);
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
