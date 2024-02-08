// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
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
        public string Content
        {
            get
            {
                return Completions[0].Data.Text;
            }
            set { }
        }

        public int PromptTokenCount
        {
            get
            {
                return Prompt.Tokens.Count;
            }
            set { }
        }

        public int CompletionTokenCount
        {
            get
            {
                return Completions[0].Data.Tokens.Count;
            }
            set { }
        }

        public int TotalTokenCount
        {
            get
            {
                return PromptTokenCount + CompletionTokenCount;
            }
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
