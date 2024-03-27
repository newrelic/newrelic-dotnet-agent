// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
            new AmazonBedrockRuntimeClient(AwsBedrockConfiguration.AwsAccessKeyId, AwsBedrockConfiguration.AwsSecretAccessKey, AwsBedrockConfiguration.AwsRegion.ToRegionId());

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
    }

    internal static class BedrockRegionExtensions
    {
        public static RegionEndpoint ToRegionId(this string region)
        {
            return RegionEndpoint.GetBySystemName(region);
        }
    }
}
