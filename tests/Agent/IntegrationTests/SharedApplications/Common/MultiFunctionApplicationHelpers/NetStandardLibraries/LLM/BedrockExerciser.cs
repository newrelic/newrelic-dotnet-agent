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
    public class BedrockExerciser
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
                // { "anthropic", BedrockModels.InvokeClaudeAsync }, // Model is EOLed as of 9/11/25
#if NET481 || NET9_0
                { "converse", BedrockModels.ConverseNovaMicro },
#endif
            };

        // Convert a flat string to a dictionary of key value pairs
        private IDictionary<string, object> ConvertAttributes(string flatDictionary)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            var kvPairs = flatDictionary.Split(',');

            foreach (var keyValue in kvPairs)
            {
                var elements = keyValue.Split('=');
                dict[elements[0]] = elements[1];
            }

            return dict;
        }


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
                throw new ArgumentException($"{model} is not a valid model");
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
                throw new ArgumentException($"{model} is not a valid model");
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModelWithFeedback(string model, string base64Prompt, string rating, string category, string message, string attributes)
        {
            if (_models.TryGetValue(model, out var func))
            {
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response = await func(prompt, false);
                Console.WriteLine(response);

                var traceId = NewRelic.Api.Agent.NewRelic.GetAgent().GetLinkingMetadata()["trace.id"];
                NewRelic.Api.Agent.NewRelic.RecordLlmFeedbackEvent(traceId, rating, category, message, ConvertAttributes(attributes));
            }
            else
            {
                throw new ArgumentException($"{model} is not a valid model");
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task InvokeModelWithCallbackAndCustomAttributes(string model, string base64Prompt, string fakeTokenCount, string attributes)
        {
            if (_models.TryGetValue(model, out var func))
            {
                NewRelic.Api.Agent.NewRelic.SetLlmTokenCountingCallback((model, message) => int.Parse(fakeTokenCount));
                foreach (var attribute in ConvertAttributes(attributes))
                {
                    NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction.AddCustomAttribute(attribute.Key, attribute.Value);
                }

                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response = await func(prompt, false);
                Console.WriteLine(response);
            }
            else
            {
                throw new ArgumentException($"{model} is not a valid model");
            }
        }

#if NET481 ||  NET9_0 // Converse API is only available in AWSSDK.BedrockRuntime v3.7.303 and later, tested by net481 and net9.0 tfms
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task Converse(string base64Prompt)
        {
            if (_models.TryGetValue("converse", out var func))
            {
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response = await func(prompt, false);
                Console.WriteLine(response);
            }
            else
            {
                throw new ArgumentException("converse is not a valid model");
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task ConverseWithError(string base64Prompt)
        {
            if (_models.TryGetValue("converse", out var func))
            {
                var bytes = Convert.FromBase64String(base64Prompt);
                var prompt = Encoding.UTF8.GetString(bytes);
                var response = await func(prompt, true);
                Console.WriteLine(response);
            }
            else
            {
                throw new ArgumentException("converse is not a valid model");
            }
        }
#endif
    }
}
