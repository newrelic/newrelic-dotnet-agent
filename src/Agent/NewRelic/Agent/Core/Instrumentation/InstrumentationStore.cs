// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Instrumentation;

public interface IInstrumentationStore
{
    bool IsEmpty { get; }
    void AddOrUpdateInstrumentation(string name, string xml);
    KeyValuePair<string, string>[] GetInstrumentation();
    bool Clear();
}

public class InstrumentationStore : IInstrumentationStore
{
    private readonly ConcurrentDictionary<string, string> _instrumentation = new ConcurrentDictionary<string, string>();

    public bool IsEmpty => _instrumentation.Count == 0;

    public void AddOrUpdateInstrumentation(string name, string xml)
    {
        if (string.IsNullOrEmpty(name))
        {
            Log.Warn($"Instrumentation {nameof(name)} was null or empty.");
            return;
        }

        _instrumentation[name] = xml;
    }

    public KeyValuePair<string, string>[] GetInstrumentation()
    {
        // The following must use ToArray because ToArray is thread safe on a ConcurrentDictionary.
        return _instrumentation.ToArray();
    }

    public bool Clear()
    {
        var instrumentationCleared = false;
        foreach (var key in _instrumentation.Keys)
        {
            _instrumentation[key] = string.Empty;
            instrumentationCleared = true;
        }

        return instrumentationCleared;
    }
}
