// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters.BedrockPayloads
{
    public class Llama2RequestPayload : IRequestPayload
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("max_gen_len")]
        public int MaxTokens { get; set; }
    }

    public class Llama2ResponsePayload : IResponsePayload
    {
        private ResponseData[] _responses;
        public ResponseData[] Responses
        {
            get
            {
                return _responses ??= [new ResponseData { Content = Generation, TokenCount = CompletionTokenCount }];
            }
            set { }
        }

        [JsonProperty("generation")]
        public string Generation { get; set; }

        [JsonProperty("prompt_token_count")]
        public int? PromptTokenCount { get; set; }

        [JsonProperty("generation_token_count")]
        public int CompletionTokenCount { get; set; }

        [JsonProperty("stop_reason")]
        public string StopReason { get; set; }
    }
}
