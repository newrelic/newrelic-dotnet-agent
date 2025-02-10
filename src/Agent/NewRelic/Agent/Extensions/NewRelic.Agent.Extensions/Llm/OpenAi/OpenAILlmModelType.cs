// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Llm.OpenAi;

/// <summary>
/// The set of models supported by the OpenAI wrapper.
/// </summary>
public enum OpenAILlmModelType
{
    Unknown,
    GPT,
    ChatGPT,
    O1,
    O3,
    DallE,
    TTS,
    Whisper,
    TextEmbedding,
    Moderation,
}
