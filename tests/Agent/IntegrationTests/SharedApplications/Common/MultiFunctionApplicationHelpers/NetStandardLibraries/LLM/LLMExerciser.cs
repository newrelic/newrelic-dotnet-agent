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
        private Dictionary<string, Func<string, bool, Task<string>>> _models =
            new Dictionary<string, Func<string, bool, Task<string>>>
            {
                { "meta13", BedrockModels.InvokeLlama213Async },
                { "meta70", BedrockModels.InvokeLlama270Async },
                { "ai21", BedrockModels.InvokeJurassicAsync },
                { "amazonembed", BedrockModels.InvokeAmazonEmbedAsync },
                { "amazonexpress", BedrockModels.InvokeAmazonExpressAsync },
                { "cohere", BedrockModels.InvokeCohereAsync },
                { "anthropic", BedrockModels.InvokeClaudeAsync },
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
                var response = await func(prompt, false);
                Console.WriteLine(response);
            }
            else
            {
                // Oopsie
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModelWithError(string model, string base64Prompt)
        {
            if (_models.TryGetValue(model, out var func))
            {
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response = await func(prompt, true);
                Console.WriteLine(response);
            }
            else
            {
                // Oopsie
            }
        }
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModelWithFeedbackAndCallback(string model, string base64Prompt, string fakeTokenCount, string rating, string category, string message, string attributes)
        {
            if (_models.TryGetValue(model, out var func))
            {

                NewRelic.Api.Agent.NewRelic.SetLlmTokenCountingCallback((model, message) => int.Parse(fakeTokenCount));
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response =await func(prompt, false);
                Console.WriteLine(response);

                var traceId = NewRelic.Api.Agent.NewRelic.GetAgent().GetLinkingMetadata()["trace.id"];

                Dictionary<string, object> dict = new Dictionary<string, object>();
                var pairs = attributes.Split(',');

                foreach (var item in pairs)
                {
                    var pair = item.Split('=');
                    dict[pair[0]] = pair[1];
                }
                NewRelic.Api.Agent.NewRelic.RecordLlmFeedbackEvent(traceId, float.Parse(rating), category, message, _attributes);
            }
            else
            {
                // Oopsie
            }
        }

    }
}
