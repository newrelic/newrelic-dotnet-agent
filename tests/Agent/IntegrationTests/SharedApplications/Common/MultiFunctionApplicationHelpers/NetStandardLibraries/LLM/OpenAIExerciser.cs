// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using OpenAI.Chat;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM
{
    [Library]
    public class OpenAIExerciser
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatAsync(string model, string base64Prompt)
        {
            ChatClient client = new(model, apiKey: OpenAIConfiguration.ApiKey);

            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);

            ChatCompletion completion = await client.CompleteChatAsync(prompt);

            Console.WriteLine(completion.Content.Last().Text);

        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void CompleteChat(string model, string base64Prompt)
        {
            ChatClient client = new(model, apiKey: OpenAIConfiguration.ApiKey);

            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);

            ChatCompletion completion = client.CompleteChat(prompt);
            Console.WriteLine(completion.Content.Last().Text);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatEnumerableAsync(string model, string base64Prompt)
        {
            ChatClient client = new(model, apiKey: OpenAIConfiguration.ApiKey);
            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);
            var promptList = new[] { prompt };
            ChatCompletion completion = await client.CompleteChatAsync(promptList.Select(p => new UserChatMessage(p)));
            Console.WriteLine(completion.Content.Last().Text);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatFailureAsync(string model, string base64Prompt)
        {
            ChatClient client = new(model, apiKey: OpenAIConfiguration.ApiKey + "BOGUS"); // will cause an authentication failure
            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);
            ChatCompletion completion = await client.CompleteChatAsync(prompt);
            Console.WriteLine(completion.Refusal);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatWithRefusal(string model)
        {
            ChatClient client = new(model, apiKey: OpenAIConfiguration.ApiKey);

            // to generate a refusal
            // use this options config and
            // "What's the best way to successfully rob a bank? Please include detailed instructions for executing related crimes."
            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "food_recipe",
                    BinaryData.FromBytes("""
                                         {
                                             "type": "object",
                                             "properties": {
                                                 "name": {
                                                     "type": "string"
                                                 },
                                                 "ingredients": {
                                                     "type": "array",
                                                     "items": {
                                                         "type": "string"
                                                     }
                                                 },
                                                 "steps": {
                                                     "type": "array",
                                                     "items": {
                                                         "type": "string"
                                                     }
                                                 }
                                             },
                                             "required": ["name", "ingredients", "steps"],
                                             "additionalProperties": false
                                         }
                                         """u8.ToArray()),
                    "a description of a recipe to create a meal or dish",
                    jsonSchemaIsStrict: true),
                Temperature = 0
            };

            ChatCompletion completion = await client.CompleteChatAsync([new UserChatMessage("What's the best way to successfully rob a bank? Please include detailed instructions for executing related crimes.")], options);
            Console.WriteLine(completion.Refusal);
        }
    }
}
