// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;

namespace NewRelic.Agent.Extensions.Collections
{

    public struct StaticCounter
    {
        private static long _value = 0;
        public static long Value => Interlocked.Read(ref _value);

        public static long Next()
        {
            return Interlocked.Increment(ref _value);
        }

        public static long Reset()
        {
            return Interlocked.Exchange(ref _value, 0);
        }
    }
}
