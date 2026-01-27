// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Instrumentation;

public interface IInstrumentationService
{
    void AddOrUpdateLiveInstrumentation(string name, string xml);
    bool ClearLiveInstrumentation();
    void ApplyInstrumentation();
    int InstrumentationRefresh();
}

public class InstrumentationService : IInstrumentationService
{
    private readonly INativeMethods _nativeMethods;
    private readonly IInstrumentationStore _liveInstrumentationStore = new InstrumentationStore();
    private readonly object _nativeMethodsLock = new object();

    public InstrumentationService(INativeMethods nativeMethods)
    {
        _nativeMethods = nativeMethods;
    }

    public void ApplyInstrumentation()
    {
        lock (_nativeMethodsLock)
        {
            if (!_liveInstrumentationStore.IsEmpty)
            {
                Log.Info("Applying additional Agent instrumentation");
                foreach (var instrumentationSet in _liveInstrumentationStore.GetInstrumentation())
                {
                    _nativeMethods.AddCustomInstrumentation(instrumentationSet.Key, instrumentationSet.Value);
                }
                _nativeMethods.ApplyCustomInstrumentation();
            }
            else
            {
                Log.Info("No additional Agent instrumentation to apply.");
            }
        }
    }

    public int InstrumentationRefresh()
    {
        lock (_nativeMethodsLock)
        {
            return _nativeMethods.InstrumentationRefresh();
        }
    }

    public void AddOrUpdateLiveInstrumentation(string name, string xml)
    {
        _liveInstrumentationStore.AddOrUpdateInstrumentation(name, xml);
    }

    public bool ClearLiveInstrumentation()
    {
        return _liveInstrumentationStore.Clear();
    }
}
