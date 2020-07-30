/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Threading;

namespace NewRelic.Agent.Core.Utilities
{
    /// <summary>
    /// A counter that can only be modified in thread-safe ways.
    /// </summary>
    public class InterlockedCounter
    {
        private int _value;
        public int Value => _value;

        public InterlockedCounter(int initialValue = 0)
        {
            _value = initialValue;
        }

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }

        public void Decrement()
        {
            Interlocked.Decrement(ref _value);
        }

        public void Add(int value)
        {
            Interlocked.Add(ref _value, value);
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
            _value = value;
        }
    }
}
