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
        private ResponseData[] _responses;
        public ResponseData[] Responses
        {
            get
            {
                return _responses ??= [new ResponseData { Content = Completion, TokenCount = null }];
            }
            set { }
        }

        [JsonPropertyName("completion")]
        public string Completion { get; set; }

        // Anthropic Claude does not expose token counts
        public int? PromptTokenCount
        {
            get
            {
                return null;
            }
            set { }
        }

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }
    }
}
