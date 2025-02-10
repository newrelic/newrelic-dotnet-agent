// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Llm.OpenAi;

public static class OpenAiHeaderDictionaryHelper
{
    public static IDictionary<string, string> GetOpenAiHeaders(this IDictionary<string, string> headers)
    {
        var llmHeaders = new Dictionary<string, string>();
        foreach (var header in headers)
        {
            switch (header.Key)
            {
                case "openai-version":
                    llmHeaders.Add(LLMConstants.Headers.LlmVersion, header.Value);
                    break;
                case "x-ratelimit-limit-requests":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitLimitRequests, header.Value);
                    break;
                case "x-ratelimit-limit-tokens":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitLimitTokens, header.Value);
                    break;
                case "x-ratelimit-remaining-requests":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitRemainingRequests, header.Value);
                    break;
                case "x-ratelimit-remaining-tokens":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitRemainingTokens, header.Value);
                    break;
                case "x-ratelimit-reset-requests":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitResetRequests, header.Value);
                    break;
                case "x-ratelimit-reset-tokens":
                    llmHeaders.Add(LLMConstants.Headers.RateLimitResetTokens, header.Value);
                    break;
            }
        }

        return llmHeaders;
    }

    public static string TryGetOpenAiOrganization(this IDictionary<string, string> headers)
    {
        if (headers.TryGetValue("openai-organization", out var organization))
        {
            return organization;
        }

        return null;
    }
}
