// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Llm
{
    /// <summary>
    /// The set of models supported by the OpenAI wrapper.
    /// </summary>
    public enum OpenAILlmModelType
    {
        GPT,
    }

    public static class OpenAILlmModelTypeExtensions
    {
        /// <summary>
        /// Converts a modelId to an LlmModelType. Throws an exception if the modelId is unknown.
        /// </summary>
        /// <param name="modelId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static OpenAILlmModelType FromOpenAIModelId(this string modelId)
        {
            if (modelId.StartsWith("gpt"))
                return OpenAILlmModelType.GPT;

            throw new Exception($"Unknown model: {modelId}");
        }
    }

    /// <summary>
    /// The set of models supported by the Bedrock wrapper.
    /// </summary>
    public enum BedrockLlmModelType
    {
        Llama2,
        CohereCommand,
        Claude,
        Titan,
        TitanEmbedded,
        Jurassic
    }

    public static class BedrockLlmModelTypeExtensions
    {
        /// <summary>
        /// Converts a modelId to an LlmModelType. Throws an exception if the modelId is unknown.
        /// </summary>
        /// <param name="modelId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static BedrockLlmModelType FromBedrockModelId(this string modelId)
        {
            if (modelId.StartsWith("meta.llama2"))
                return BedrockLlmModelType.Llama2;

            if (modelId.StartsWith("cohere.command"))
                return BedrockLlmModelType.CohereCommand;

            if (modelId.StartsWith("anthropic.claude"))
                return BedrockLlmModelType.Claude;

            if (modelId.StartsWith("amazon.titan-text"))
                return BedrockLlmModelType.Titan;

            if (modelId.StartsWith("amazon.titan-embed-text"))
                return BedrockLlmModelType.TitanEmbedded;

            if (modelId.StartsWith("ai21.j2"))
                return BedrockLlmModelType.Jurassic;

            throw new Exception($"Unknown model: {modelId}");
        }
    }

}
