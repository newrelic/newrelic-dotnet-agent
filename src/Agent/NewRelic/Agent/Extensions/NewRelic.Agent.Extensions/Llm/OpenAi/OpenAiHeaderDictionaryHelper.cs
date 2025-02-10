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
                    llmHeaders.Add("llmVersion", header.Value);
                    break;
                case "x-ratelimit-limit-requests:":
                    llmHeaders.Add("ratelimitLimitRequests", header.Value);
                    break;
                case "x-ratelimit-limit-tokens":
                    llmHeaders.Add("ratelimitLimitTokens", header.Value);
                    break;
                case "x-ratelimit-remaining-requests":
                    llmHeaders.Add("ratelimitRemainingRequests", header.Value);
                    break;
                case "x-ratelimit-remaining-tokens":
                    llmHeaders.Add("ratelimitRemainingTokens", header.Value);
                    break;
                case "x-ratelimit-reset-requests":
                    llmHeaders.Add("ratelimitResetRequests", header.Value);
                    break;
                case "x-ratelimit-reset-tokens":
                    llmHeaders.Add("ratelimitResetTokens", header.Value);
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
