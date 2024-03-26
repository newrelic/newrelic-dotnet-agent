// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters.BedrockPayloads
{
    public class JurassicRequestPayload : IRequestPayload
    {
        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("temperature")]
        public float Temperature { get; set; }

        [JsonProperty("maxTokens")]
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

        [JsonProperty("prompt")]
        public PromptData Prompt { get; set; }

        public class PromptData
        {
            [JsonProperty("tokens")]
            public List<object> Tokens { get; set; }
        }

        [JsonProperty("completions")]
        public List<Completion> Completions { get; set; }

        public class Completion
        {
            [JsonProperty("data")]
            public Data Data { get; set; }

            [JsonProperty("finishReason")]
            public FinishReason FinishReason { get; set; }
        }

        public class Data
        {
            [JsonProperty("text")]
            public string Text { get; set; }

            [JsonProperty("tokens")]
            public List<object> Tokens { get; set; }
        }

        public class FinishReason
        {
            [JsonProperty("reason")]
            public string Reason { get; set; }
        }
    }
}
