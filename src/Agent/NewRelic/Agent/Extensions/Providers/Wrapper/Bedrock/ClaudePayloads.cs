// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class ClaudeRequestPayload : IRequestPayload
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens_to_sample")]
        public int MaxTokens { get; set; }
    }

    public class ClaudeResponsePayload : IResponsePayload
    {
        private string[] _responses;
        public string[] Responses
        {
            get
            {
                return _responses ??= [Completion];
            }
            set { }
        }

        [JsonPropertyName("completion")]
        public string Completion { get; set; }

        // Anthropic Claude does not expose token counts
        public int PromptTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        // Anthropic Claude does not expose token counts
        public int CompletionTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        // Anthropic Claude does not expose token counts
        public int TotalTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }
    }
}
