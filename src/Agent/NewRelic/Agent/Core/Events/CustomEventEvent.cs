// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.Events;

public class CustomEventEvent
{
    public readonly string EventType;

    public readonly IEnumerable<KeyValuePair<string, object>> Attributes;

    public CustomEventEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
    {
        EventType = eventType;
        Attributes = attributes;
    }
}