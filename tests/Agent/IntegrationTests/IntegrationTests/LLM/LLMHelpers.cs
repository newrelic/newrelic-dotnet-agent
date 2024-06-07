// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;

namespace NewRelic.Agent.IntegrationTests.LLM
{
    [Flags]
    public enum LlmMessageTypes
    {
        None = 0,
        LlmChatCompletionSummary = 1,
        LlmChatCompletionMessage = 2,
        LlmEmbedding = 4,
        All = LlmChatCompletionSummary | LlmChatCompletionMessage | LlmEmbedding
    }
    public class LLMHelpers
    {
        public static string ConvertToBase64(string prompt) => HeaderEncoder.Base64Encode(prompt);
    }

    public static class LLMExtensions
    {
        public static object SafeGetAttribute(this CustomEventData evt, string attribute)
        {
            if (evt?.Attributes.TryGetValue(attribute, out object val) == true)
            {
                return val;
            }
            return null;
        }
    }
}
