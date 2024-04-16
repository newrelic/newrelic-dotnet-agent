// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NewRelic.Core.JsonConverters.BedrockPayloads
{
    public class TitanRequestPayload : IRequestPayload
    {
        [JsonProperty("inputText")]
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

        [JsonProperty("textGenerationConfig")]
        public TextGenerationConfigData TextGenerationConfig { get; set; }

        public class TextGenerationConfigData
        {
            [JsonProperty("maxTokenCount")]
            public int TokenCount { get; set; }

            [JsonProperty("temperature")]
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
        [JsonProperty("embeddings")]
        private List<float> Embeddings { get; set; }

        [JsonProperty("inputTextTokenCount")]
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

        [JsonProperty("inputTextTokenCount")]
        public int? PromptTokenCount { get; set; }

        public string StopReason
        {
            get
            {
                return Results[0].CompletionReason;
            }
            set { }
        }

        [JsonProperty("results")]
        public List<Result> Results { get; set; }

        public class Result
        {
            [JsonProperty("tokenCount")]
            public int TokenCount { get; set; }

            [JsonProperty("outputText")]
            public string OutputText { get; set; }

            [JsonProperty("completionReason")]
            public string CompletionReason { get; set; }
        }
    }
}
