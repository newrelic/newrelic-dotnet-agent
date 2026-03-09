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
using OpenAI;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM;

[Library]
public class MicrosoftExtensionsAIExerciser
{
    private const string AzureModel = "integration-test-gpt-4o-mini"; // the only model available for Azure testing
    private const string OpenAIModel = "gpt-4o";
    private const string ActivitySource = "Experimental.Microsoft.Extensions.AI";

    private IChatClient CreateAzureChatClient(bool useBogusKey = false)
    {
        var endpoint = new Uri(AzureOpenAiConfiguration.Endpoint);
        var apiKey = useBogusKey ? AzureOpenAiConfiguration.ApiKey + "BOGUS" : AzureOpenAiConfiguration.ApiKey;
        var credentials = new ApiKeyCredential(apiKey);

        var azureClient = new AzureOpenAIClient(endpoint, credentials);
        var chatClient = azureClient.GetChatClient(AzureModel);

        return WrapWithOpenTelemetry(chatClient);
    }

    private IChatClient CreateOpenAIChatClient(bool useBogusKey = false)
    {
        var apiKey = useBogusKey ? OpenAIConfiguration.ApiKey + "BOGUS" : OpenAIConfiguration.ApiKey;
        var credentials = new ApiKeyCredential(apiKey);

        var openAIClient = new OpenAIClient(credentials);
        var chatClient = openAIClient.GetChatClient(OpenAIModel);

        return WrapWithOpenTelemetry(chatClient);
    }

    private IChatClient WrapWithOpenTelemetry(OpenAI.Chat.ChatClient chatClient)
    {
        return new ChatClientBuilder(chatClient.AsIChatClient())
            .UseOpenTelemetry(sourceName: ActivitySource, configure: options =>
            {
                options.EnableSensitiveData = true; // this is necessary to see all the data we need to build our LLM events
            })
            .Build();
    }

    private IChatClient CreateChatClient(string provider, bool useBogusKey = false)
    {
        return provider.Equals("openai", StringComparison.OrdinalIgnoreCase)
            ? CreateOpenAIChatClient(useBogusKey)
            : CreateAzureChatClient(useBogusKey);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task CompleteChatAsync(string provider, string base64Prompt)
    {
        var chatClient = CreateChatClient(provider);

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        var response = await chatClient.GetResponseAsync(prompt);

        Console.WriteLine(response.Text);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task CompleteChatStreamingAsync(string provider, string base64Prompt)
    {
        var chatClient = CreateChatClient(provider);

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
    public async Task CompleteChatFailureAsync(string provider, string base64Prompt)
    {
        var chatClient = CreateChatClient(provider, useBogusKey: true);

        var bytes = Convert.FromBase64String(base64Prompt);
        var prompt = Encoding.UTF8.GetString(bytes);

        try
        {
            var response = await chatClient.GetResponseAsync(prompt);
            Console.WriteLine(response.Text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Expected exception occurred: {ex.Message}");
            throw;
        }
    }
}

#endif
