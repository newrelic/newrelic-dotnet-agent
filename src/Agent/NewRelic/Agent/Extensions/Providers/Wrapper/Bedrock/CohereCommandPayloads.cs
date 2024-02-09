// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class CohereCommandRequestPayload : IRequestPayload
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    public class CohereCommandResponsePayload : IResponsePayload
    {
        private string[] _responses;
        public string[] Responses
        {
            get
            {
                return _responses ??= Generations.Select(g => g.Text).ToArray();
            }
            set { }
        }

        // Cohere Command does not expose token counts
        public int PromptTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        // Cohere Command does not expose token counts
        public int CompletionTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        // Cohere Command does not expose token counts
        public int TotalTokenCount
        {
            get
            {
                return 0;
            }
            set { }
        }

        public string StopReason
        {
            get
            {
                return Generations[0].FinishReason;
            }
            set { }
        }

        [JsonPropertyName("generations")]
        public List<Generation> Generations { get; set; }

        public partial class Generation
        {
            [JsonPropertyName("finish_reason")]
            public string FinishReason { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }
        }
    }
}
