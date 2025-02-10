// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Llm.OpenAi;

public static class OpenAILlmModelTypeExtensions
{
    /// <summary>
    /// Converts a modelId to an LlmModelType. Throws an exception if the modelId is unknown.
    /// </summary>
    /// <param name="modelId"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static OpenAILlmModelType FromOpenAiModelId(this string modelId)
    {
        return modelId switch
        {
            var id when id.StartsWith("gpt") => OpenAILlmModelType.GPT,
            var id when id.StartsWith("chat") => OpenAILlmModelType.ChatGPT,
            var id when id.StartsWith("o1") => OpenAILlmModelType.O1,
            var id when id.StartsWith("o3") => OpenAILlmModelType.O3,
            var id when id.StartsWith("dall-e") => OpenAILlmModelType.DallE,
            var id when id.StartsWith("tts") => OpenAILlmModelType.TTS,
            var id when id.StartsWith("whisper") => OpenAILlmModelType.Whisper,
            var id when id.StartsWith("text-embedding") => OpenAILlmModelType.TextEmbedding,
            var id when id.Contains("moderation") => OpenAILlmModelType.Moderation,
            _ => OpenAILlmModelType.Unknown,
        };
    }
}
