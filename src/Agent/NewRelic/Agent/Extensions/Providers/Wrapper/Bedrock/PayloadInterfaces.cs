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
        string[] Responses { get; set; }

        int TotalTokenCount { get; }

        int PromptTokenCount { get; set; }

        int CompletionTokenCount { get; set; }

        string StopReason { get; set; }
    }
}
