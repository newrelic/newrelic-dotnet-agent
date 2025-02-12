// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Llm.Bedrock;

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
