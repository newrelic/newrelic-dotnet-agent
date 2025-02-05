// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Llm;
using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads
{
    public class OpenAiRequestPayload : IOpenAiRequestPayload
    {
        public string Prompt { get; set; }
    }

    public class OpenAiResponsePayload : IOpenAiResponsePayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        //[JsonProperty("choices")]
        //public ChoicesObj[] Choices { get; set; }

        [JsonProperty("usage")]
        public UsageObj Usage { get; set; }

        [JsonProperty("system_fingerprint")]
        public string SystemFingerprint { get; set; }
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
}
