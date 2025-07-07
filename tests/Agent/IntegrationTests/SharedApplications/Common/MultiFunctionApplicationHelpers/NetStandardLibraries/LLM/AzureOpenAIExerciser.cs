// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.ClientModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using OpenAI.Chat;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM
{
    [Library]
    public class AzureOpenAIExerciser
    {
        private const string Model = "integration-test-gpt-4o-mini"; // the only model available for testing

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatAsync(string base64Prompt)
        {
            var endpoint = new Uri(AzureOpenAiConfiguration.Endpoint);
            var credentials = new ApiKeyCredential(AzureOpenAiConfiguration.ApiKey);

            AzureOpenAIClient azureClient = new(endpoint, credentials);
            ChatClient client = azureClient.GetChatClient(Model);

            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);

            ChatCompletion completion = await client.CompleteChatAsync(prompt);

            Console.WriteLine(completion.Content.Last().Text);

        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void CompleteChat(string base64Prompt)
        {
            var endpoint = new Uri(AzureOpenAiConfiguration.Endpoint);
            var credentials = new ApiKeyCredential(AzureOpenAiConfiguration.ApiKey);

            AzureOpenAIClient azureClient = new(endpoint, credentials);
            ChatClient client = azureClient.GetChatClient(Model);

            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);

            ChatCompletion completion = client.CompleteChat(prompt);
            Console.WriteLine(completion.Content.Last().Text);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task CompleteChatFailureAsync(string base64Prompt)
        {
            var endpoint = new Uri(AzureOpenAiConfiguration.Endpoint);
            var credentials = new ApiKeyCredential(AzureOpenAiConfiguration.ApiKey+"BOGUS");

            AzureOpenAIClient azureClient = new(endpoint, credentials);
            ChatClient client = azureClient.GetChatClient(Model);

            var bytes = Convert.FromBase64String(base64Prompt);
            var prompt = Encoding.UTF8.GetString(bytes);
            ChatCompletion completion = await client.CompleteChatAsync(prompt);
            Console.WriteLine(completion.Refusal);
        }
    }
}
