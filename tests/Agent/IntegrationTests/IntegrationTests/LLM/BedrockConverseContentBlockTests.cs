// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.LLM;

// Coverage for Bedrock Converse responses whose Content[] contains non-text blocks. These scenarios
// exercise the response-emittable content block types (Text, ReasoningContent, ToolUse) plus the
// request-side ToolResult block. They live in their own fixture, separate from the basic happy-path
// and error coverage in BedrockConverseTests, because:
//   - the basic test's global assertions (CallCountAllHarvests, single error event) would otherwise
//     have to track every scenario's call count, and
//   - these responses have distinct shapes (a leading ReasoningContent block, an empty-text response
//     on the tool_use turn, two ConverseAsync calls per tool invocation) that should not bleed into
//     the basic test's whole-log assertions.
public abstract class BedrockConverseContentBlockTestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;

    // Prompts have spaces in them, which will not be parsed correctly, so they are base64-encoded.
    private readonly string _thinkingPrompt = "In one sentence, what is a large-language model?";
    private readonly string _toolPrompt = "What is the current weather in Seattle? Use the get_weather tool.";

    private const string ExtendedThinkingTransaction =
        "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.BedrockExerciser/ConverseWithExtendedThinking";
    private const string ToolUseTransaction =
        "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.BedrockExerciser/ConverseWithToolUseAndThinking";

    protected BedrockConverseContentBlockTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(3));
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
                // Two transactions: the extended-thinking call and the (two-turn) tool-use call.
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(3), 2);
            }
        );

        _fixture.AddCommand($"BedrockExerciser ConverseWithExtendedThinking {LLMHelpers.ConvertToBase64(_thinkingPrompt)}");
        _fixture.AddCommand($"BedrockExerciser ConverseWithToolUseAndThinking {LLMHelpers.ConvertToBase64(_toolPrompt)}");

        _fixture.Initialize();
    }

    // Scopes custom (LLM) events to a single transaction by correlating on trace id. Both scenarios use
    // the same model, so the model name cannot disambiguate them; the transaction's trace id can.
    private List<CustomEventData> GetLlmEventsForTransaction(string transactionName)
    {
        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(transactionName);
        Assert.NotNull(transactionEvent);

        var traceId = transactionEvent.IntrinsicAttributes["traceId"]?.ToString();
        Assert.False(string.IsNullOrEmpty(traceId), $"Transaction '{transactionName}' had no traceId");

        return _fixture.AgentLog.GetCustomEvents()
            .Where(ce => ce.Attributes.TryGetValue("trace_id", out var t) && t?.ToString() == traceId)
            .ToList();
    }

    private static CustomEventData ResponseMessage(IEnumerable<CustomEventData> messageEvents) =>
        messageEvents.SingleOrDefault(m => m.Attributes.TryGetValue("is_response", out var r) && r is bool b && b);

    // Extended thinking: the response's Content[0] is a ReasoningContent block and Content[1] is the Text
    // block. Verifies the call is fully instrumented and the response text is extracted from past the
    // leading non-text block (the original bug dropped the whole call here).
    [Fact]
    public void ConverseExtendedThinkingTest()
    {
        var events = GetLlmEventsForTransaction(ExtendedThinkingTransaction);

        var summaries = events.Where(e => e.Header.Type == "LlmChatCompletionSummary").ToList();
        var messages = events.Where(e => e.Header.Type == "LlmChatCompletionMessage").ToList();

        Assert.Single(summaries);
        Assert.Equal(2, messages.Count); // prompt + response

        var responseMessage = ResponseMessage(messages);
        Assert.NotNull(responseMessage);
        Assert.True(responseMessage.Attributes.TryGetValue("content", out var content) && !string.IsNullOrEmpty(content?.ToString()),
            "Extended-thinking response message content should be non-empty (text extracted past the ReasoningContent block)");
    }

    // Tool use with extended thinking is a two-turn exchange instrumented as two ConverseAsync calls in
    // one transaction:
    //   Turn 1 response: [ReasoningContent, ToolUse]  (no text - empty response content is expected)
    //   Turn 2 response: [Text]                       (the model's final answer, after the ToolResult)
    [Fact]
    public void ConverseToolUseTest()
    {
        var events = GetLlmEventsForTransaction(ToolUseTransaction);

        var summaries = events.Where(e => e.Header.Type == "LlmChatCompletionSummary").ToList();
        var messages = events.Where(e => e.Header.Type == "LlmChatCompletionMessage").ToList();

        Assert.Equal(2, summaries.Count);   // turn 1 + turn 2
        Assert.Equal(4, messages.Count);    // each turn: prompt + response

        // At least one response message carries the model's final text answer (turn 2). The turn-1
        // response is a tool_use turn with no text block, so its response content is legitimately empty.
        var responseMessages = messages.Where(m => m.Attributes.TryGetValue("is_response", out var r) && r is bool b && b).ToList();
        Assert.Equal(2, responseMessages.Count);
        Assert.Contains(responseMessages, m =>
            m.Attributes.TryGetValue("content", out var c) && !string.IsNullOrEmpty(c?.ToString()));
    }

    // Both scenarios contribute ConverseAsync completion segments: 1 (extended thinking) + 2 (tool use).
    [Fact]
    public void ConverseContentBlockMetricsTest()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Custom/Llm/completion/Bedrock/ConverseAsync", CallCountAllHarvests = 3 },
            new() { metricName = @"Supportability/DotNet/ML/.*", IsRegexName = true },
            new() { metricName = @"Supportability/DotNet/LLM/.*/.*", IsRegexName = true },
            new() { metricName = @"Supportability/DotNet/LLM/Bedrock-Converse" },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        Assertions.MetricsExist(expectedMetrics, metrics);
    }
}

public class BedrockConverseContentBlockTests_CoreLatest : BedrockConverseContentBlockTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public BedrockConverseContentBlockTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class BedrockConverseContentBlockTests_FWLatest : BedrockConverseContentBlockTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public BedrockConverseContentBlockTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
