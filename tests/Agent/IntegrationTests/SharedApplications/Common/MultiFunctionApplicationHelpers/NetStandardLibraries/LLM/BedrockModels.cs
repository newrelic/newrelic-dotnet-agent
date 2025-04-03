// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Amazon.BedrockRuntime.Model;
using Amazon.BedrockRuntime;
using Amazon.Util;
using Amazon;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LLM
{
    internal class BedrockModels
    {
        private static readonly AmazonBedrockRuntimeClient _amazonBedrockRuntimeClient =
            new AmazonBedrockRuntimeClient(AwsConfiguration.AwsAccessKeyId, AwsConfiguration.AwsSecretAccessKey, AwsConfiguration.AwsRegion.ToRegionId());

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeAmazonEmbedAsync(string prompt, bool generateError) => await InvokeTitanAsync(prompt, true, generateError);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeAmazonExpressAsync(string prompt, bool generateErrort) => await InvokeTitanAsync(prompt, false, generateErrort);


        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeLlama213Async(string prompt, bool generateError)
        {
            string payload = new JsonObject()
            {
                { "prompt", prompt },
                { "max_gen_len", generateError ? -1 : 512 },
                { "temperature", 0.5 },
                { "top_p", 0.9 }
            }.ToJsonString();

            string generatedText = "";
            try
            {

                var response = await InvokeModel("meta.llama2-13b-chat-v1", payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        return node?["generation"]?.GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeLlama270Async(string prompt, bool generateError)
        {
            string payload = new JsonObject()
            {
                { "prompt", prompt },
                { "max_gen_len", generateError ? -1 : 512 },
                { "temperature", 0.5 },
                { "top_p", 0.9 }
            }.ToJsonString();

            string generatedText = "";
            try
            {

                var response = await InvokeModel("meta.llama2-70b-chat-v1", payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        return node?["generation"]?.GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<string> InvokeTitanAsync(string prompt, bool isEmbed, bool generateError)
        {
            string payload = "";
            if (isEmbed)
            {
                payload = new JsonObject()
        {
            { "inputText", prompt }
        }.ToJsonString();
            }
            else
            {
                payload = new JsonObject()
            {
                { "inputText", prompt },
                { "textGenerationConfig", new JsonObject()
                    {
                        { "maxTokenCount", 512 },
                        { "temperature", 0.5 },
                        { "topP", 0.9 },
                        { "stopSequences", new JsonArray() }
                    }
                }
            }.ToJsonString();
            }

            string generatedText = "";
            try
            {
                var model = isEmbed ? "amazon.titan-embed-text-v1" : "amazon.titan-text-express-v1";

                var response = await InvokeModel(model, payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        if (isEmbed)
                        {
                            return "EMBED";
                        }

                        return node?["results"]?
                            .AsArray()[0]
                            ["outputText"].GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeJurassicAsync(string prompt, bool generateError)
        {
            string payload = new JsonObject()
            {
                { "prompt", prompt },
                { "temperature", 0.5 },
                { "topP", 0.9 },
                { "maxTokens", 512 },
                { "numResults", 2 }
        }.ToJsonString();

            string generatedText = "";
            try
            {

                var response = await InvokeModel("ai21.j2-mid-v1", payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        return node?["completions"]?
                            .AsArray()[0]
                            ["data"]
                            ["text"].GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeClaudeAsync(string prompt, bool generateError)
        {
            string payload = new JsonObject()
            {
                { "prompt", "\n\nHuman: " + prompt + "\n\nAssistant:"},
                { "temperature", 0.5 },
                { "top_p", 0.9 },
                { "max_tokens_to_sample", 512 }
            }.ToJsonString();

            string generatedText = "";
            try
            {

                var response = await InvokeModel("anthropic.claude-v2", payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        return node?["completion"]?.GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> InvokeCohereAsync(string prompt, bool generateError)
        {
            string payload = new JsonObject()
            {
                { "prompt", prompt },
                { "max_tokens", 512 },
                { "temperature", 0.5 },
                { "p", 0.9 }
            }.ToJsonString();

            string generatedText = "";
            try
            {

                var response = await InvokeModel("cohere.command-text-v14", payload);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    using (StreamReader reader = new(response.Body))
                    {
                        var body = await reader.ReadToEndAsync();
                        var node = JsonNode.Parse(body);
                        return node?["generations"]?[0]["text"].GetValue<string>() ?? "";
                    }
                }
                else
                {
                    Console.WriteLine("InvokeModelAsync failed with status code " + response.HttpStatusCode);
                }
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e.Message);
            }
            return generatedText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<InvokeModelResponse> InvokeModel(string model, string payload)
        {
            return await _amazonBedrockRuntimeClient.InvokeModelAsync(new InvokeModelRequest()
            {
                ModelId = model,
                Body = AWSSDKUtils.GenerateMemoryStreamFromString(payload),
                ContentType = "application/json",
                Accept = "application/json"
            });
        }

#if NET481 ||  NET10_0 // Converse API is only available in AWSSDK.BedrockRuntime v3.7.303 and later, tested by net481 and net10.0 tfms
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static async Task<string> ConverseNovaMicro(string prompt, bool generateError)
        {
            string responseText = "";
            try
            {
                var response = await Converse("us.amazon.nova-micro-v1:0" + (generateError ? "bogus" : ""), prompt);

                responseText = response?.Output?.Message?.Content?[0]?.Text ?? "";
            }
            catch (AmazonBedrockRuntimeException e)
            {
                Console.WriteLine(e);
            }
            return responseText;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<ConverseResponse> Converse(string model, string payload)
        {

            // Create a request with the model ID, the user message, and an inference configuration.
            var request = new ConverseRequest
            {
                ModelId = model,

                Messages =
                [
                    new Message { Role = ConversationRole.User, Content = [new ContentBlock { Text = payload }] }
                ],
                InferenceConfig = new InferenceConfiguration
                {
                    MaxTokens = 512,
                    Temperature = 0.5F,
                    TopP = 0.9F
                }
            };

            return await _amazonBedrockRuntimeClient.ConverseAsync(request);
        }
#endif
    }

    internal static class BedrockRegionExtensions
    {
        public static RegionEndpoint ToRegionId(this string region)
        {
            return RegionEndpoint.GetBySystemName(region);
        }
    }
}
