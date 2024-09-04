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
    public abstract class BedrockTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        // Prompts have spaces in them, which will not be parsed correctly
        private string _prompt = "In one sentence, what is a large-language model?";
        private List<string> _bedrockModelsToTest = new List<string>
        {
            "meta13",
            "amazonembed",
            "amazonexpress",
            "cohere",
            "anthropic"
        };

        private Dictionary<string, LlmMessageTypes> _expectedAttributes = new Dictionary<string, LlmMessageTypes>
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

        public BedrockTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), _bedrockModelsToTest.Count);
                }
            );

            foreach (var model in _bedrockModelsToTest)
            {
                _fixture.AddCommand($"LLMExerciser InvokeModel {model} {LLMHelpers.ConvertToBase64(_prompt)}");
            }

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
        public void BedrockTest()
        {
            var expectedMetrics = new List<Assertions.ExpectedMetric>
            {
                new Assertions.ExpectedMetric { metricName = @"Custom/Llm/completion/Bedrock/InvokeModelAsync", CallCountAllHarvests = _bedrockModelsToTest.Count - 1 },
                new Assertions.ExpectedMetric { metricName = @"Custom/Llm/embedding/Bedrock/InvokeModelAsync", CallCountAllHarvests = 1 },
                new Assertions.ExpectedMetric { metricName = @"Supportability/DotNet/ML/.*", IsRegexName = true}
            };

            var customEvents = _fixture.AgentLog.GetCustomEvents().ToList();
            ValidateCommonAttributes(customEvents);

            var metrics = _fixture.AgentLog.GetMetrics().ToList();
            Assertions.MetricsExist(expectedMetrics, metrics);

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.LLMExerciser/InvokeModel");

            Assert.NotNull( transactionEvent );
        }
    }

    [NetCoreTest]
    public class BedrockTests_Basic_CoreLatest : BedrockTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public BedrockTests_Basic_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class BedrockTests_Basic_FWLatest : BedrockTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public BedrockTests_Basic_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
