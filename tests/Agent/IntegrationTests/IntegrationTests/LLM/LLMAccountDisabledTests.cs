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
    public abstract class LlmAccountDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private const string _model = "meta13";
        private string _prompt = "In one sentence, what is a large-language model?";

        public LlmAccountDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;
            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .EnableAiMonitoring(true) // must be true to test override.
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(3), 2);
                }
            );

            _fixture.AddCommand($"BedrockExerciser InvokeModel {_model} {LLMHelpers.ConvertToBase64(_prompt)}");

            _fixture.Initialize();
        }

        //[Fact] // The model we were using that was marked as disabled is deprecated, so the test no longer works
        public void BedrockDisabledTest()
        {
            // Make sure it actually got called
            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.BedrockExerciser/InvokeModel");
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
    public class LlmAccountDisabledTest_CoreLatest : LlmAccountDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatestAIM>
    {
        public LlmAccountDisabledTest_CoreLatest(ConsoleDynamicMethodFixtureCoreLatestAIM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class LlmAccountDisabledTest_FWLatest : LlmAccountDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatestAIM>
    {
        public LlmAccountDisabledTest_FWLatest(ConsoleDynamicMethodFixtureFWLatestAIM fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
