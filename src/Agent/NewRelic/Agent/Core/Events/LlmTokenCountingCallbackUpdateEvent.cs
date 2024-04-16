// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Events
{
    public class LlmTokenCountingCallbackUpdateEvent
    {
        public readonly Func<string, string, int> LlmTokenCountingCallback;

        public LlmTokenCountingCallbackUpdateEvent(Func<string, string, int> llmTokenCountingCallback)
        {
            LlmTokenCountingCallback = llmTokenCountingCallback;
        }
    }
}
