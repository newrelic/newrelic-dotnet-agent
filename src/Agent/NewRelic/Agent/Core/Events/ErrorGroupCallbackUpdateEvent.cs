// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events
{
    public class ErrorGroupCallbackUpdateEvent
    {
        public readonly Func<IDictionary<string, object>, string> ErrorGroupCallback;

        public ErrorGroupCallbackUpdateEvent(Func<IDictionary<string, object>, string> errorGroupCallback)
        {
            ErrorGroupCallback = errorGroupCallback;
        }
    }
}
