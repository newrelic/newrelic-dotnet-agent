// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.Tracing;

namespace NewRelic.Agent.Core.Samplers;

public interface ISampledEventListener<T> : IDisposable
{
    T Sample();
    void StopListening();
    void StartListening();
}

public abstract class SampledEventListener<T> : EventListener, ISampledEventListener<T>
{
    public abstract T Sample();

    public static readonly object ListenerLock = new object();

    protected EventSource _eventSource;

    public void StopListening()
    {
        if (_eventSource != null)
        {
            lock (ListenerLock)
            {
                DisableEvents(_eventSource);
            }
        }
    }

    public abstract void StartListening();

    public override void Dispose()
    {
        lock (ListenerLock)
        {
            base.Dispose();
        }
    }
}