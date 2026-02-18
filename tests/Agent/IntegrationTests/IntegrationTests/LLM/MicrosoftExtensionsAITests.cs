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

public abstract class MicrosoftExtensionsAITestsBase<TFixture> : NewRelicIntegrationTest<TFixture> where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;

    private readonly string _prompt = "In one sentence, what is a large-language model?";

    private readonly string _actvitySourceName = "Experimental.Microsoft.Extensions.AI";

    // Attributes expected through the OTEL bridge path (ProcessLLMChatClientTags).
    // This is a subset of the OpenAI wrapper attributes because the OTEL path has no
    // access to response headers, organization, or request_id.
    private readonly Dictionary<string, LlmMessageTypes> _expectedAttributes = new()
    {
        {"id", LlmMessageTypes.All},
        {"span_id", LlmMessageTypes.All},
        {"trace_id", LlmMessageTypes.All},
        {"request.model", LlmMessageTypes.LlmChatCompletionSummary},
        {"response.model", LlmMessageTypes.All},
        {"response.number_of_messages", LlmMessageTypes.LlmChatCompletionSummary},
        {"response.choices.finish_reason", LlmMessageTypes.LlmChatCompletionSummary},
        {"vendor", LlmMessageTypes.All},
        {"ingest_source", LlmMessageTypes.All},
        {"duration", LlmMessageTypes.LlmChatCompletionSummary},
        {"content", LlmMessageTypes.LlmChatCompletionMessage},
        {"role", LlmMessageTypes.LlmChatCompletionMessage},
        {"sequence", LlmMessageTypes.LlmChatCompletionMessage},
        {"completion_id", LlmMessageTypes.LlmChatCompletionMessage},
        {"token_count", LlmMessageTypes.LlmChatCompletionMessage},
    };

    protected MicrosoftExtensionsAITestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        // Queue exerciser commands
        _fixture.AddCommand($"MicrosoftExtensionsAIExerciser CompleteChatAsync {LLMHelpers.ConvertToBase64(_prompt)}");
        _fixture.AddCommand($"MicrosoftExtensionsAIExerciser CompleteChatStreamingAsync {LLMHelpers.ConvertToBase64(_prompt)}");
        _fixture.AddCommand($"MicrosoftExtensionsAIExerciser CompleteChat {LLMHelpers.ConvertToBase64(_prompt)}");
        _fixture.AddCommand($"MicrosoftExtensionsAIExerciser CompleteChatFailureAsync {LLMHelpers.ConvertToBase64(_prompt)}");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
                    .EnableAiMonitoring()
                    .EnableOpenTelemetry(true)
                    .EnableOpenTelemetryTracing(true)
                    .IncludeActivitySource(_actvitySourceName)
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

            // Verify no unexpected attributes for this event type
            foreach (var pair in evt.Attributes)
            {
                if (_expectedAttributes.TryGetValue(pair.Key, out var expectedTypes))
                {
                    Assert.True(expectedTypes.HasFlag(type), $"{type} is not expected to have the attribute {pair.Key}");
                }
            }

            // Verify all required attributes are present
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
    public void MicrosoftExtensionsAITest()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Supportability/DotNet/ML/.*", IsRegexName = true },
            new() { metricName = @"Supportability/DotNet/LLM/.*", IsRegexName = true },
        };

        var customEventsSuccess = _fixture.AgentLog.GetCustomEvents()
            .Where(ce => !ce.Attributes.Keys.Contains("error")).ToList();
        var customEventFailure = _fixture.AgentLog.GetCustomEvents()
            .SingleOrDefault(ce => ce.Attributes.Keys.Contains("error"));

        var applicationErrorEvent = _fixture.AgentLog.GetErrorEvents().SingleOrDefault();

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var transactionEventAsync = _fixture.AgentLog.TryGetTransactionEvent(
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.MicrosoftExtensionsAIExerciser/CompleteChatAsync");
        var transactionEventStreaming = _fixture.AgentLog.TryGetTransactionEvent(
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.MicrosoftExtensionsAIExerciser/CompleteChatStreamingAsync");
        var transactionEventSync = _fixture.AgentLog.TryGetTransactionEvent(
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.MicrosoftExtensionsAIExerciser/CompleteChat");
        var transactionEventFailure = _fixture.AgentLog.TryGetTransactionEvent(
            "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.MicrosoftExtensionsAIExerciser/CompleteChatFailureAsync");

        Assert.Multiple(() =>
        {
            Assertions.MetricsExist(expectedMetrics, metrics);
            Assert.NotNull(transactionEventAsync);
            Assert.NotNull(transactionEventStreaming);
            Assert.NotNull(transactionEventSync);

            Assert.Equal(3, customEventsSuccess.Count);
            ValidateCommonAttributes(customEventsSuccess);

            Assert.NotNull(customEventFailure);
            Assert.Equal(true, customEventFailure.Attributes["error"]);

            // Verify the failure event is a LlmChatCompletionSummary
            Assert.Equal("LlmChatCompletionSummary", customEventFailure.Header.Type);

            // Verify the failure event is correlated to the CompleteChatFailureAsync transaction
            Assert.NotNull(transactionEventFailure);
            var expectedTraceId = transactionEventFailure.IntrinsicAttributes["traceId"]?.ToString();
            Assert.True(customEventFailure.Attributes.ContainsKey("trace_id"));
            Assert.Equal(expectedTraceId, customEventFailure.Attributes["trace_id"]?.ToString());

            Assert.NotNull(applicationErrorEvent);
            Assert.Contains("completion_id", applicationErrorEvent.UserAttributes.Keys);
            Assert.Contains("http.statusCode", applicationErrorEvent.UserAttributes.Keys);
        });
    }
}

// CoreLatest only - MEAI packages require OpenAI >= 2.8.0 which is only
// available in MFALatestPackages (net10.0). The net8.0 target has OpenAI 2.0.0
// which is incompatible with Microsoft.Extensions.AI.OpenAI.
public class MicrosoftExtensionsAITests_CoreLatest : MicrosoftExtensionsAITestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MicrosoftExtensionsAITests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
