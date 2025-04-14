// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    public abstract class BedrockConverseTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
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
            {"request.temperature", LlmMessageTypes.LlmChatCompletionSummary},
            {"request.max_tokens", LlmMessageTypes.LlmChatCompletionSummary},
            {"request.model", LlmMessageTypes.LlmChatCompletionSummary | LlmMessageTypes.LlmEmbedding},
            {"response.model", LlmMessageTypes.All},
            {"response.number_of_messages", LlmMessageTypes.LlmChatCompletionSummary},
            {"response.choices.finish_reason", LlmMessageTypes.LlmChatCompletionSummary},
            {"vendor", LlmMessageTypes.All},
            {"ingest_source", LlmMessageTypes.All},
            {"duration", LlmMessageTypes.LlmChatCompletionSummary | LlmMessageTypes.LlmEmbedding},
            {"content", LlmMessageTypes.LlmChatCompletionMessage},
            {"role", LlmMessageTypes.LlmChatCompletionMessage},
            {"sequence", LlmMessageTypes.LlmChatCompletionMessage},
            {"completion_id", LlmMessageTypes.LlmChatCompletionMessage},
            {"input", LlmMessageTypes.LlmEmbedding},
        };

        protected BedrockConverseTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(2));
            _fixture.TestLogger = output;
            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .ForceTransactionTraces()
                        .EnableAiMonitoring()
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 2);
                }
            );

            _fixture.AddCommand($"BedrockExerciser Converse {LLMHelpers.ConvertToBase64(_prompt)}");
            _fixture.AddCommand($"BedrockExerciser ConverseWithError {LLMHelpers.ConvertToBase64(_prompt)}");

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
        public void ConverseTest()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new() { metricName = @"Custom/Llm/completion/Bedrock/ConverseAsync", CallCountAllHarvests = 2 },
                new() { metricName = @"Supportability/DotNet/ML/.*", IsRegexName = true},
                new() { metricName = @"Supportability/DotNet/LLM/.*/.*", IsRegexName = true} // Supportability/DotNet/LLM/{vendor}/{model}
            };

            var customEventsSuccess = _fixture.AgentLog.GetCustomEvents().Where(ce => !ce.Attributes.Keys.Contains("error")).ToList();
            var customEventFailure = _fixture.AgentLog.GetCustomEvents().SingleOrDefault(ce => ce.Attributes.Keys.Contains("error"));

            ValidateCommonAttributes(customEventsSuccess);
            Assert.NotNull(customEventFailure);
            Assert.Equal(true, customEventFailure.Attributes["error"]);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.BedrockExerciser/Converse");
            Assert.NotNull( transactionEvent );

            var transactionEventError = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.BedrockExerciser/ConverseWithError");
            Assert.NotNull(transactionEventError);

            var applicationErrorEvent = _fixture.AgentLog.GetErrorEvents().SingleOrDefault();
            Assert.NotNull(applicationErrorEvent);
            Assert.Contains("completion_id", applicationErrorEvent.UserAttributes.Keys);
            Assert.Contains("http.statusCode", applicationErrorEvent.UserAttributes.Keys);
            Assert.Contains("error.code", applicationErrorEvent.UserAttributes.Keys);
        }
    }

    public class BedrockConverseTests_Basic_CoreLatest : BedrockConverseTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public BedrockConverseTests_Basic_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    public class BedrockConverseTests_Basic_FWLatest : BedrockConverseTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public BedrockConverseTests_Basic_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
