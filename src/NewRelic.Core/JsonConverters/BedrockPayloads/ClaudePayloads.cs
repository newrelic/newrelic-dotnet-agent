// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters.BedrockPayloads
{
    public class ClaudeRequestPayload : IRequestPayload
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("max_tokens_to_sample")]
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

        [JsonProperty("completion")]
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

        [JsonProperty("stop_reason")]
        public string StopReason { get; set; }
    }
}
