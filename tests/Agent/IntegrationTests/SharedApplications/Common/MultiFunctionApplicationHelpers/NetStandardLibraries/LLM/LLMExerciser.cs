// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM
{
    [Library]
    public class LLMExerciser
    {
        private Dictionary<string, Func<string, Task<string>>> _models =
            new Dictionary<string, Func<string, Task<string>>>
            {
                { "meta13", BedrockModels.InvokeLlama213Async },
                { "meta70", BedrockModels.InvokeLlama270Async },
                { "ai21", BedrockModels.InvokeJurassicAsync },
                { "amazonembed", BedrockModels.InvokeAmazonEmbedAsync },
                { "amazonexpress", BedrockModels.InvokeAmazonExpressAsync },
                { "cohere", BedrockModels.InvokeCohereAsync },
                { "anthropic", BedrockModels.InvokeClaudeAsync },
                { "badllama", BedrockModels.InvokeBadLlama2Async },
            };

        Dictionary<string, object> _attributes = new Dictionary<string, object>
        {
            { "foo", "bar" },
            { "number", 123 }
        };

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModel(string model, string base64Prompt)
        {
            if (_models.TryGetValue(model, out var func))
            {
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                await func(prompt);
            }
            else
            {
                // Oopsie
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModelWithFeedbackAndCallback(string model, string base64Prompt, string fakeTokenCount)
        {
            if (_models.TryGetValue(model, out var func))
            {

                NewRelic.Api.Agent.NewRelic.SetLlmTokenCountingCallback((model, message) => int.Parse(fakeTokenCount));
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                await func(prompt);

                var traceId = NewRelic.Api.Agent.NewRelic.GetAgent().GetLinkingMetadata()["trace.id"];

                NewRelic.Api.Agent.NewRelic.RecordLlmFeedbackEvent(traceId, 2.0, "mycategory", "good job", _attributes);
            }
            else
            {
                // Oopsie
            }
        }

    }
}
