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
    public abstract class LlmApiTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private const string _feedbackModel = "meta13";
        private const string _attributeModel = "ai21";
        private string _prompt = "In one sentence, what is a large-language model?";
        private const long _fakeTokenCount = 42;
        private const string _rating = "3.14";
        private const string _category = "mycategory";
        private const string _message = "good_job";
        private const string _feedbackAttributes = "foo=bar,number=123";
        private const string _customAttributes = "llm.account=11235,llm.month=january,llm.year=2024,drop=this,and=that";

        public LlmApiTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
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

            // Setting the token count callback will result in us dumping the event queues, so do that first
            _fixture.AddCommand($"LLMExerciser InvokeModelWithCallbackAndCustomAttributes {_attributeModel} {LLMHelpers.ConvertToBase64(_prompt)} {_fakeTokenCount} {_customAttributes}");
            _fixture.AddCommand($"LLMExerciser InvokeModelWithFeedback {_feedbackModel} {LLMHelpers.ConvertToBase64(_prompt)} {_rating} {_category} {_message} {_feedbackAttributes}");

            _fixture.Initialize();
        }

        [Fact]
        public void BedrockApiTest()
        {
            bool found = false;
            var customEvents = _fixture.AgentLog.GetCustomEvents();

            // Check that the callback for calculating the token count worked and the custom attributes were added to the transaction event
            var apiEvents = customEvents.Where(evt => (evt.Attributes.ContainsKey("response.model") && evt.Attributes["response.model"].ToString().StartsWith("ai21"))).ToList();
            foreach (var apiEvent in apiEvents)
            {
                if (apiEvent.Attributes.TryGetValue("token_count", out var count))
                {
                    Assert.Equal(_fakeTokenCount, count);
                    found = true;
                }
                Assert.Equal("11235", apiEvent.SafeGetAttribute("llm.account"));
                Assert.Equal("january", apiEvent.SafeGetAttribute("llm.month"));
                Assert.Equal("2024", apiEvent.SafeGetAttribute("llm.year"));

                // Only attributes with the prefix "llm." should be added to the transaction event
                Assert.False(apiEvent.Attributes.ContainsKey("drop"));
                Assert.False(apiEvent.Attributes.ContainsKey("and"));
                Assert.False(apiEvent.Attributes.ContainsKey("llm.drop"));
                Assert.False(apiEvent.Attributes.ContainsKey("llm.and"));
            }
            Assert.True(found, "Could not find token count");

            var feedback = customEvents.Where(evt => evt.Header.Type == "LlmFeedbackMessage").FirstOrDefault();

            Assert.NotNull(feedback);
            Assert.Equal("bar", feedback.SafeGetAttribute("foo"));
            Assert.Equal("123", feedback.SafeGetAttribute("number"));
            Assert.Equal("3.14", feedback.SafeGetAttribute("rating"));
            Assert.Equal("mycategory", feedback.SafeGetAttribute("category"));
            Assert.Equal("good_job", feedback.SafeGetAttribute("message"));

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.LLMExerciser/InvokeModelWithCallbackAndCustomAttributes");

            Assert.NotNull(transactionEvent);
        }
    }
    [NetCoreTest]
    public class LlmApiTest_CoreLatest : LlmApiTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public LlmApiTest_CoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class LlmApiTest_FWLatest : LlmApiTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public LlmApiTest_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

}
