// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace NewRelic.Agent.Extensions.SystemExtensions.Collections
{
    public static class NameValueCollectionExtensions
    {
        /// <summary>
        /// Creates a dictionary from a name value collection by iterating over all of keys and values. The key will be case-insensitive.
        /// </summary>
        /// <param name="nameValueCollection"></param>
        /// <param name="equalityComparer"></param>
        /// <returns></returns>

        public static IDictionary<string, string> ToDictionary(this NameValueCollection nameValueCollection, IEqualityComparer<string> equalityComparer = null)
        {
            equalityComparer = equalityComparer ?? StringComparer.CurrentCultureIgnoreCase;

            return nameValueCollection
                .Keys
                .Cast<string>()
                .Where(key => key != null)
                .ToDictionary(key => key, key => nameValueCollection[key], equalityComparer);
        }
    }
}
