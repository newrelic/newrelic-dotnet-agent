// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET10_0_OR_GREATER

using System;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM;

[Library]
public class MicrosoftExtensionsAIExerciser
{
    private const string Model = "integration-test-gpt-4o-mini"; // the only model available for testing

    private IChatClient CreateChatClient(bool useBogusKey = false)
    {
        var endpoint = new Uri(AzureOpenAiConfiguration.Endpoint);
        var apiKey = useBogusKey ? AzureOpenAiConfiguration.ApiKey + "BOGUS" : AzureOpenAiConfiguration.ApiKey;
        var credentials = new ApiKeyCredential(apiKey);

        var azureClient = new AzureOpenAIClient(endpoint, credentials);
        var openAIChatClient = azureClient.GetChatClient(Model);

        // Wrap the OpenAI ChatClient with MEAI and enable OpenTelemetry instrumentation
        IChatClient chatClient = new ChatClientBuilder(openAIChatClient.AsIChatClient())
            .UseOpenTelemetry()
            .Build();

        return chatClient;
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task CompleteChatAsync(string base64Prompt)
    {
        var chatClient = CreateChatClient();

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        var response = await chatClient.GetResponseAsync(prompt);

        Console.WriteLine(response.Text);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public void CompleteChat(string base64Prompt)
    {
        var chatClient = CreateChatClient();

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        var response = chatClient.GetResponseAsync(prompt).GetAwaiter().GetResult();

        Console.WriteLine(response.Text);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task CompleteChatStreamingAsync(string base64Prompt)
    {
        var chatClient = CreateChatClient();

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        var responseText = new StringBuilder();

        await foreach (var update in chatClient.GetStreamingResponseAsync(prompt))
        {
            if (update.Text != null)
            {
                responseText.Append(update.Text);
            }
        }

        Console.WriteLine(responseText.ToString());
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task CompleteChatFailureAsync(string base64Prompt)
    {
        var chatClient = CreateChatClient(useBogusKey: true);

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        var response = await chatClient.GetResponseAsync(prompt);

        Console.WriteLine(response.Text);
    }
}

#endif
