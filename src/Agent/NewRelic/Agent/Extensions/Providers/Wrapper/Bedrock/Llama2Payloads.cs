// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class Llama2RequestPayload : IRequestPayload
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_gen_len")]
        public int MaxTokens { get; set; }
    }

    public class Llama2ResponsePayload : IResponsePayload
    {
        [JsonPropertyName("generation")]
        public string Content { get; set; }

        [JsonPropertyName("prompt_token_count")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("generation_token_count")]
        public int CompletionTokenCount { get; set; }

        public int TotalTokenCount
        {
            get
            {
                return PromptTokenCount + CompletionTokenCount;
            }
        }

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }
    }
}
