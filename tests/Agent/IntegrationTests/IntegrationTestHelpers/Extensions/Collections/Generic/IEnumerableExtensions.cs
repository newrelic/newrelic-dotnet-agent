// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers.Collections.Generic
{
    public static class IEnumerableExtensions
    {


        public static IEnumerable<T> ForEachLazy<T>(this IEnumerable<T> source, Action<T> action)
        {
            return new ForEachEnumerable<T>(source, action);
        }

        public static void ForEachNow<T>(this IEnumerable<T> source, Action<T> action)
        {
            var enumerable = source.ForEachLazy(action);
            foreach (var item in enumerable)
            {
                // do nothing, we just want to force enumeration
            }
        }
    }
}
