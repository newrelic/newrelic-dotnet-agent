// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public interface IRequestPayload
    {
        string Prompt { get; set; }

        float Temperature { get; set; }

        int MaxTokens { get; set; }
    }

    public interface IResponsePayload
    {
        ResponseData[] Responses { get; set; }

        int? PromptTokenCount { get; set; }

        string StopReason { get; set; }
    }

    public class ResponseData
    {
        public string Content { get; set; }

        public int? TokenCount { get; set; }
    }
}
