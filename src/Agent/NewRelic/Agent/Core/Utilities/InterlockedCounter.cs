// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A counter that can only be modified in thread-safe ways.
/// </summary>
[System.Diagnostics.DebuggerDisplay("{_value}")]
public class InterlockedCounter
{
    private int _value;
    public int Value => _value;

    public InterlockedCounter(int initialValue = 0)
    {
        _value = initialValue;
    }

    public int Increment()
    {
        return Interlocked.Increment(ref _value);
    }

    public int Decrement()
    {
        return Interlocked.Decrement(ref _value);
    }

    public int Add(int value)
    {
        return Interlocked.Add(ref _value, value);
    }

    public int Exchange(int value)
    {
        return Interlocked.Exchange(ref _value, value);
    }

    public int CompareExchange(int value, int comparand)
    {
        return Interlocked.CompareExchange(ref _value, value, comparand);
    }

    public void Set(int value)
    {
        Exchange(value);
    }
}

[System.Diagnostics.DebuggerDisplay("{_value}")]
public class InterlockedLongCounter
{
    private long _value;
    public long Value => _value;

    public InterlockedLongCounter(long initialValue = 0)
    {
        _value = initialValue;
    }

    public long Increment()
    {
        return Interlocked.Increment(ref _value);
    }

    public long Decrement()
    {
        return Interlocked.Decrement(ref _value);
    }

    public long Add(int value)
    {
        return Interlocked.Add(ref _value, value);
    }

    public long Add(uint value)
    {
        return Interlocked.Add(ref _value, value);
    }

    public long Add(long value)
    {
        return Interlocked.Add(ref _value, value);
    }

    public long Add(ulong value)
    {
        var valueAsLong = value > long.MaxValue ? long.MaxValue : (long)value;
        return Interlocked.Add(ref _value, valueAsLong);
    }

    public long Exchange(long value)
    {
        return Interlocked.Exchange(ref _value, value);
    }

    public long CompareExchange(long value, long comparand)
    {
        return Interlocked.CompareExchange(ref _value, value, comparand);
    }

    public void Set(long value)
    {
        Exchange(value);
    }
}