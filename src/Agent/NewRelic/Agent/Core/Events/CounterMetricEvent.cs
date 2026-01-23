// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Events;

public class CounterMetricEvent
{
    public readonly string Namespace;
    public readonly string Name;
    public readonly int Count;

    public CounterMetricEvent(string @namespace, string name, int count = 1)
    {
        Namespace = @namespace;
        Name = name;
        Count = count;
    }
    public CounterMetricEvent(string name, int count = 1)
    {
        Namespace = "";
        Name = name;
        Count = count;
    }
}