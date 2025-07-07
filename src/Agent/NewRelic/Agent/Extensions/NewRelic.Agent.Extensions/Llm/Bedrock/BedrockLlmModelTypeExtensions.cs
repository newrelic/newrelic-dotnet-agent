// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Llm.Bedrock;

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
