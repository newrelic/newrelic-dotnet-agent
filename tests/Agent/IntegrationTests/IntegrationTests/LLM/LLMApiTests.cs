// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    public abstract class LlmApiTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;
        private const string _model = "meta13";
        private string _prompt = "In one sentence, what is a large-language model?";
        private const long _fakeTokenCount = 42;

        public LlmApiTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(20));
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
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(20), 1);
                }
            );

            _fixture.AddCommand($"LLMExerciser InvokeModelWithFeedbackAndCallback {_model} {LLMHelpers.ConvertToBase64(_prompt)} {_fakeTokenCount}");
            _fixture.AddCommand($"RootCommands DelaySeconds 10");

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            bool found = false;
            var customEvents = _fixture.AgentLog.GetCustomEvents();
            var feedback = customEvents.Where(evt => evt.Header.Type == "LlmFeedbackMessage").FirstOrDefault();

            Assert.NotNull(feedback);
            Assert.Equal("bar", feedback.SafeGetAttribute("foo"));
            Assert.Equal((long)123, feedback.SafeGetAttribute("number"));
            Assert.Equal(3.14, feedback.SafeGetAttribute("rating"));
            Assert.Equal("mycategory", feedback.SafeGetAttribute("category"));
            Assert.Equal("good job", feedback.SafeGetAttribute("message"));

            foreach (var customEvent in customEvents)
            {
                if (customEvent.Attributes.TryGetValue("token_count", out var count))
                {
                    Assert.Equal(count, _fakeTokenCount);
                    found = true;
                }
            }
            Assert.True(found, "Could not find token count");

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent($"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.LLM.LLMExerciser/InvokeModelWithFeedbackAndCallback");

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
