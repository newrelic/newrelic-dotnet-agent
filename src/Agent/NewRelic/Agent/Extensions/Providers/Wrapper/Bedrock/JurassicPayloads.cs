// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class JurassicRequestPayload : IRequestPayload
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; }
    }

    public class JurassicResponsePayload : IResponsePayload
    {
        private ResponseData[] _responses;
        public ResponseData[] Responses
        {
            get
            {
                return _responses ??= Completions.Select(c => new ResponseData { Content = c.Data.Text, TokenCount = c.Data.Tokens.Count }).ToArray();
            }
            set { }
        }

        public int? PromptTokenCount
        {
            get
            {
                return Prompt.Tokens.Count;
            }
            set { }
        }

        public string StopReason
        {
            get
            {
                return Completions[0].FinishReason.Reason;
            }
            set { }
        }

        [JsonPropertyName("prompt")]
        public PromptData Prompt { get; set; }

        public class PromptData
        {
            [JsonPropertyName("tokens")]
            public List<object> Tokens { get; set; }
        }

        [JsonPropertyName("completions")]
        public List<Completion> Completions { get; set; }

        public class Completion
        {
            [JsonPropertyName("data")]
            public Data Data { get; set; }

            [JsonPropertyName("finishReason")]
            public FinishReason FinishReason { get; set; }
        }

        public class Data
        {
            [JsonPropertyName("text")]
            public string Text { get; set; }

            [JsonPropertyName("tokens")]
            public List<object> Tokens { get; set; }
        }

        public class FinishReason
        {
            [JsonPropertyName("reason")]
            public string Reason { get; set; }
        }
    }
}
