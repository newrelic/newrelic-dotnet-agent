// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.LLM;

// No AI monitoring setting is configured, so basic telemetry is on by default: LLM events and metrics
// are sent, but the message content and the embedding input are not.
public abstract class LlmBasicTelemetryDefaultTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private const string _transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.OpenAIExerciser/CompleteChatAsync";
    private readonly TFixture _fixture;
    private readonly string _prompt = "In one sentence, what is a large-language model?";

    protected LlmBasicTelemetryDefaultTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"OpenAIExerciser CompleteChatAsync gpt-4o {LLMHelpers.ConvertToBase64(_prompt)}");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
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

    [Fact]
    public void BasicTelemetrySendsLlmDataWithoutContent()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"^Supportability/DotNet/ML/openai/[^/]+/Basic$", IsRegexName = true },
            new() { metricName = @"Custom/Llm/completion/openai/CompleteChatAsync" },
        };

        // The version metric without the /Basic suffix marks content recording.
        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"^Supportability/DotNet/ML/openai/[^/]+$", IsRegexName = true },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var customEvents = _fixture.AgentLog.GetCustomEvents();
        var summaryEvents = customEvents.Where(evt => evt.Header.Type == "LlmChatCompletionSummary").ToList();
        var messageEvents = customEvents.Where(evt => evt.Header.Type == "LlmChatCompletionMessage").ToList();
        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_transactionName);

        Assert.Multiple(() =>
        {
            Assert.NotNull(transactionEvent);
            Assertions.MetricsExist(expectedMetrics, metrics);
            Assertions.MetricsDoNotExist(unexpectedMetrics, metrics);

            Assert.NotEmpty(summaryEvents);
            Assert.NotEmpty(messageEvents);
            Assert.All(messageEvents, evt =>
            {
                Assert.False(evt.Attributes.ContainsKey("content"), "LlmChatCompletionMessage must not carry content when only basic telemetry is on");
                Assert.True(evt.Attributes.ContainsKey("role"), "LlmChatCompletionMessage should still carry role");
                Assert.True(evt.Attributes.ContainsKey("completion_id"), "LlmChatCompletionMessage should still carry completion_id");
            });
        });
    }
}

// Basic telemetry is turned off and ai_monitoring.enabled is unset, so no AI monitoring data is sent.
public abstract class LlmBasicTelemetryDisabledTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private const string _transactionName = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.OpenAIExerciser/CompleteChatAsync";
    private readonly TFixture _fixture;
    private readonly string _prompt = "In one sentence, what is a large-language model?";

    protected LlmBasicTelemetryDisabledTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _fixture.AddCommand($"OpenAIExerciser CompleteChatAsync gpt-4o {LLMHelpers.ConvertToBase64(_prompt)}");

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
                    .EnableAiMonitoringBasicTelemetry(false)
                    .ConfigureFasterMetricsHarvestCycle(12)
                    .SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForMetricAggregateCallCount("OtherTransaction/all", 1, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void BasicTelemetryDisabledSendsNoLlmData()
    {
        var unexpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Supportability/DotNet/ML/openai/.*", IsRegexName = true },
            new() { metricName = @"Custom/Llm/.*", IsRegexName = true },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var llmEvents = _fixture.AgentLog.GetCustomEvents().Where(evt => evt.Header.Type.StartsWith("Llm")).ToList();
        var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(_transactionName);

        Assert.Multiple(() =>
        {
            // Make sure the instrumented method actually ran
            Assert.NotNull(transactionEvent);
            Assertions.MetricsDoNotExist(unexpectedMetrics, metrics);
            Assert.Empty(llmEvents);
        });
    }
}

public class LlmBasicTelemetryDefaultTest_CoreLatest : LlmBasicTelemetryDefaultTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public LlmBasicTelemetryDefaultTest_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class LlmBasicTelemetryDefaultTest_FWLatest : LlmBasicTelemetryDefaultTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public LlmBasicTelemetryDefaultTest_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class LlmBasicTelemetryDisabledTest_CoreLatest : LlmBasicTelemetryDisabledTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public LlmBasicTelemetryDisabledTest_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class LlmBasicTelemetryDisabledTest_FWLatest : LlmBasicTelemetryDisabledTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public LlmBasicTelemetryDisabledTest_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
