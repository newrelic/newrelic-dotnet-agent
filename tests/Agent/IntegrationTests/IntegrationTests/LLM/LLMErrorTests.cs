// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    public abstract class LlmErrorTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private const string _accessDeniedModel = "meta70";
        private const string _badConfigModel = "meta13";
        private string _prompt = "In one sentence, what is a large-language model?";

        public LlmErrorTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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

            _fixture.AddCommand($"LLMExerciser InvokeModel {_accessDeniedModel} {LLMHelpers.ConvertToBase64(_prompt)}");
            _fixture.AddCommand($"LLMExerciser InvokeModelWithError {_badConfigModel} {LLMHelpers.ConvertToBase64(_prompt)}");

            _fixture.Initialize();
        }

        [Fact]
        public void BedrockErrorTest()
        {
            var customEvents = _fixture.AgentLog.GetCustomEvents();

            var payloads = _fixture.AgentLog.GetErrorEventPayloads();
            int errors = 0;
            foreach (var payload in payloads)
            {
                foreach (var error in payload.Events)
                {
                    Assert.NotNull(error.UserAttributes["error.code"]);
                    Assert.NotNull(error.UserAttributes["http.statusCode"]);
                    Assert.True(error.UserAttributes.ContainsKey("completion_id") || error.UserAttributes.ContainsKey("embedding_id"));
                    errors++;
                }
            }
            Assert.Equal(2, errors);

            var promptEvents = customEvents.Where(evt => evt.Header.Type == "LlmChatCompletionMessage");
            Assert.Equal(2, promptEvents.Count());

            var completionEvents = customEvents.Where(evt => evt.Header.Type == "LlmChatCompletionSummary");
            Assert.Equal(2, completionEvents.Count());
            foreach (var evt  in completionEvents)
            {
                Assert.True((bool)evt.SafeGetAttribute("error"));
            }

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.LLMExerciser/InvokeModelWithError");

            Assert.NotNull(transactionEvent);
        }
    }
    [NetCoreTest]
    public class LlmErrorTests_CoreLatest : LlmErrorTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public LlmErrorTests_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class LlmErrorTests_FWLatest : LlmErrorTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public LlmErrorTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
