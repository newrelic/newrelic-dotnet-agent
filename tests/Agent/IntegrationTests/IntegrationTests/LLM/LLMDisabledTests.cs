// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    public abstract class LlmDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private const string _model = "meta13";
        private string _prompt = "In one sentence, what is a large-language model?";

        public LlmDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;
            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .EnableAiMonitoring(false)
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2), 2);
                }
            );

            _fixture.AddCommand($"LLMExerciser InvokeModel {_model} {LLMHelpers.ConvertToBase64(_prompt)}");

            _fixture.Initialize();
        }

        [Fact]
        public void BedrockDisabledTest()
        {
            // Make sure it actually got called
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.LLMExerciser/InvokeModel");
            Assert.NotNull(transactionEvent);

            var unexpectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Supportability/DotNet/ML/Bedrock/.*", IsRegexName = true },
                new Assertions.ExpectedMetric { metricName = @"Custom/Llm/.*", IsRegexName = true },
            };

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assertions.MetricsDoNotExist(unexpectedMetrics, metrics);


        }
    }
    [NetCoreTest]
    public class LlmDisabledTest_CoreLatest : LlmDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public LlmDisabledTest_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class LlmDisabledTest_FWLatest : LlmDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public LlmDisabledTest_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
