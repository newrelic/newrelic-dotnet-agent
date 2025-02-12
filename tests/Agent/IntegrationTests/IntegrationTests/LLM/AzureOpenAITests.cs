// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    public abstract class AzureOpenAITestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        // Prompts have spaces in them, which will not be parsed correctly
        private string _prompt = "In one sentence, what is a large-language model?";

        private Dictionary<string, LlmMessageTypes> _expectedAttributes = new()
        {
            {"id", LlmMessageTypes.All},
            {"request_id", LlmMessageTypes.All},
            {"span_id", LlmMessageTypes.All},
            {"trace_id", LlmMessageTypes.All},
            {"request.model", LlmMessageTypes.LlmChatCompletionSummary | LlmMessageTypes.LlmEmbedding},
            // not available in Azure.AI.OpenAI {"response.headers.llmVersion", LlmMessageTypes.LlmChatCompletionSummary},
            // not available in Azure.AI.OpenAI {"response.headers.ratelimitLimitRequests", LlmMessageTypes.LlmChatCompletionSummary},
            // not available in Azure.AI.OpenAI {"response.headers.ratelimitLimitTokens", LlmMessageTypes.LlmChatCompletionSummary},
            // not available in Azure.AI.OpenAI {"response.headers.ratelimitResetRequests", LlmMessageTypes.LlmChatCompletionSummary},
            // not available in Azure.AI.OpenAI {"response.headers.ratelimitResetTokens", LlmMessageTypes.LlmChatCompletionSummary},
            {"response.headers.ratelimitRemainingRequests", LlmMessageTypes.LlmChatCompletionSummary},
            {"response.headers.ratelimitRemainingTokens", LlmMessageTypes.LlmChatCompletionSummary},
            {"response.model", LlmMessageTypes.All},
            {"response.number_of_messages", LlmMessageTypes.LlmChatCompletionSummary},
            {"response.choices.finish_reason", LlmMessageTypes.LlmChatCompletionSummary},
            // not available in Azure.AI.OpenAI {"response.organization", LlmMessageTypes.LlmChatCompletionSummary},
            {"vendor", LlmMessageTypes.All},
            {"ingest_source", LlmMessageTypes.All},
            {"duration", LlmMessageTypes.LlmChatCompletionSummary | LlmMessageTypes.LlmEmbedding},
            {"content", LlmMessageTypes.LlmChatCompletionMessage},
            {"role", LlmMessageTypes.LlmChatCompletionMessage},
            {"sequence", LlmMessageTypes.LlmChatCompletionMessage},
            {"token_count", LlmMessageTypes.LlmChatCompletionMessage},
            {"completion_id", LlmMessageTypes.LlmChatCompletionMessage},
            {"input", LlmMessageTypes.LlmEmbedding},
        };

        protected AzureOpenAITestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"AzureOpenAIExerciser CompleteChatAsync {LLMHelpers.ConvertToBase64(_prompt)}");
            _fixture.AddCommand($"AzureOpenAIExerciser CompleteChat {LLMHelpers.ConvertToBase64(_prompt)}");
            _fixture.AddCommand($"AzureOpenAIExerciser CompleteChatFailureAsync {LLMHelpers.ConvertToBase64(_prompt)}");


            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .EnableAiMonitoring()
                        .ConfigureFasterTransactionTracesHarvestCycle(10)
                        .ConfigureFasterMetricsHarvestCycle(12)
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();
        }

        private void ValidateCommonAttributes(IEnumerable<CustomEventData> customEvents)
        {
            foreach (var evt in customEvents)
            {
                if (!Enum.TryParse<LlmMessageTypes>(evt.Header.Type, out var type))
                {
                    Assert.Fail($"{evt.Header.Type} is not a recognized LLM message type");
                }

                foreach (var pair in evt.Attributes)
                {
                    if (_expectedAttributes.TryGetValue(pair.Key, out var expectedTypes))
                    {
                        Assert.True(expectedTypes.HasFlag(type), $"{type} is not expected to have the attribute {pair.Key}");
                    }
                }

                foreach (var pair in _expectedAttributes)
                {
                    if (pair.Value.HasFlag(type))
                    {
                        if (!evt.Attributes.TryGetValue(pair.Key, out _))
                        {
                            Assert.Fail($"The attribute '{pair.Key}' is expected in an '{type}' event but it was not found");
                        }
                    }
                }
            }
        }

        [Fact]
        public void AzureOpenAITest()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = @"Custom/Llm/completion/azureopenai/CompleteChatAsync" },
                new() { metricName = @"Custom/Llm/completion/azureopenai/CompleteChatAsync", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.AzureOpenAIExerciser/CompleteChatAsync"},
                new() { metricName = @"Custom/Llm/completion/azureopenai/CompleteChat" },
                new() { metricName = @"Custom/Llm/completion/azureopenai/CompleteChat", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.AzureOpenAIExerciser/CompleteChat"},
                new() { metricName = @"Supportability/DotNet/ML/.*", IsRegexName = true}
            };

            var customEventsSuccess = _fixture.AgentLog.GetCustomEvents().Where(ce => !ce.Attributes.Keys.Contains("error")).ToList();
            var customEventFailure = _fixture.AgentLog.GetCustomEvents().SingleOrDefault(ce => ce.Attributes.Keys.Contains("error"));

            var applicationErrorEvent = _fixture.AgentLog.GetErrorEvents().SingleOrDefault();

            var metrics = _fixture.AgentLog.GetMetrics().ToList();

            var transactionEventAsync = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.AzureOpenAIExerciser/CompleteChatAsync");
            var transactionEventSync = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.AzureOpenAIExerciser/CompleteChat");

            Assert.Multiple(() =>
            {
                Assertions.MetricsExist(expectedMetrics, metrics);
                Assert.NotNull(transactionEventAsync);
                Assert.NotNull(transactionEventSync);
                ValidateCommonAttributes(customEventsSuccess);

                Assert.NotNull(customEventFailure);
                Assert.Equal(true, customEventFailure.Attributes["error"]);

                Assert.NotNull(applicationErrorEvent);
                Assert.Contains("completion_id", applicationErrorEvent.UserAttributes.Keys);
                Assert.Contains("http.statusCode", applicationErrorEvent.UserAttributes.Keys);
            });
        }
    }

    [NetCoreTest]
    public class AzureOpenAITests_CoreLatest : AzureOpenAITestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public AzureOpenAITests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class AzureOpenAITests_CoreOldest : AzureOpenAITestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public AzureOpenAITests_CoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class AzureOpenAITests_FWLatest : AzureOpenAITestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public AzureOpenAITests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
