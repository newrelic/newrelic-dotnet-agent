// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Llm
{
    public static class LLMConstants
    {
        public static class Headers
        {
            public const string LlmVersion = "llmVersion";
            public const string RateLimitLimitRequests = "ratelimitLimitRequests";
            public const string RateLimitLimitTokens = "ratelimitLimitTokens";
            public const string RateLimitRemainingRequests = "ratelimitRemainingRequests";
            public const string RateLimitRemainingTokens = "ratelimitRemainingTokens";
            public const string RateLimitResetRequests = "ratelimitResetRequests";
            public const string RateLimitResetTokens = "ratelimitResetTokens";
            public const string RateLimitLimitTokensUsageBased = "ratelimitLimitTokensUsageBased";
            public const string RateLimitRemainingTokensUsageBased = "ratelimitRemainingTokensUsageBased";
            public const string RateLimitResetTokensUsageBased = "ratelimitResetTokensUsageBased";
        }
    }
}
