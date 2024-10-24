// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads
{
    public class GPTRequestPayload : IRequestPayload
    {
        [JsonProperty("messages")]
        public MessageObj[] Messages { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        /*[JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("max_tokens_to_sample")]
        public int MaxOutputTokenCount { get; set; }*/
    }

    public class GPTResponsePayload : IResponsePayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("choices")]
        public ChoicesObj[] Choices { get; set; }
        
        [JsonProperty("usage")]
        public UsageObj Usage { get; set; }

        [JsonProperty("system_fingerprint")]
        public string SystemFingerprint { get; set; }
    }

    public class ChoicesObj
    {
        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("logprobs")]
        public string LogProbs { get; set; }

        [JsonProperty("finish_reason")]
        public string FinishReason { get; set; }

        [JsonProperty("message")]
        public MessageObj Message { get; set; }
    }

    public class MessageObj
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("refusal")]
        public string Refusal { get; set; }
    }

    public class UsageObj
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonProperty("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonProperty("total_tokens")]
        public int TotalTokens { get; set; }
    }

    /*public class GPTResponsePayloadOld : IResponsePayload
    {
        private ResponseData[] _responses;
        public ResponseData[] Content
        {
            get
            {
                return _responses ??= [new ResponseData { Text = Completion }];
            }
            set { }
        }

        [JsonProperty("completion")]
        public string Completion { get; set; }

        public int? PromptTokenCount
        {
            get
            {
                return null;
            }
            set { }
        }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("stop_reason")]
        public string StopReason { get; set; }

        public ResponseUsage Usage { get; set; }
    }*/
}
