// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events
{
    public class ErrorGroupCallbackUpdateEvent
    {
        public readonly Func<Exception, string> ErrorGroupCallback;

        public ErrorGroupCallbackUpdateEvent(Func<Exception, string> errorGroupCallback)
        {
            ErrorGroupCallback = errorGroupCallback;
        }
    }
}
