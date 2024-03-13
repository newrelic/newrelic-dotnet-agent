// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NewRelic.Providers.Wrapper.Bedrock.Payloads
{
    public class TitanRequestPayload : IRequestPayload
    {
        [JsonPropertyName("inputText")]
        public string Prompt { get; set; }

        public float Temperature
        {
            get
            {
                return TextGenerationConfig.Temperature;
            }
            set { }
        }

        public int MaxTokens
        {
            get
            {
                return TextGenerationConfig.TokenCount;
            }
            set { }
        }

        [JsonPropertyName("textGenerationConfig")]
        public TextGenerationConfigData TextGenerationConfig { get; set; }

        public class TextGenerationConfigData
        {
            [JsonPropertyName("maxTokenCount")]
            public int TokenCount { get; set; }

            [JsonPropertyName("temperature")]
            public float Temperature { get; set; }
        }
    }

    /*
     {
        "embedding": [float, float, ...],
        "inputTextTokenCount": int
     }
     */

    public class TitanEmbeddedResponsePayload : IResponsePayload
    {
        [JsonPropertyName("embeddings")]
        private List<float> Embeddings { get; set; }

        [JsonPropertyName("inputTextTokenCount")]
        public int? PromptTokenCount { get; set; }

        public ResponseData[] Responses { get => null; set { } }

        public string StopReason { get => "FINISHED"; set { } }
    }

    public class TitanResponsePayload : IResponsePayload
    {
        private ResponseData[] _responses;
        public ResponseData[] Responses
        {
            get
            {
                return _responses ??= Results.Select(r => new ResponseData { Content = r.OutputText, TokenCount = r.TokenCount }).ToArray();
            }
            set { }
        }

        [JsonPropertyName("inputTextTokenCount")]
        public int? PromptTokenCount { get; set; }

        public string StopReason
        {
            get
            {
                return Results[0].CompletionReason;
            }
            set { }
        }

        [JsonPropertyName("results")]
        public List<Result> Results { get; set; }

        public class Result
        {
            [JsonPropertyName("tokenCount")]
            public int TokenCount { get; set; }

            [JsonPropertyName("outputText")]
            public string OutputText { get; set; }

            [JsonPropertyName("completionReason")]
            public string CompletionReason { get; set; }
        }
    }
}
