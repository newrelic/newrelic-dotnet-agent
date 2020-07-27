using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.SystemExtensions.Collections
{
    public static class NameValueCollectionExtensions
    {
        /// <summary>
        /// Creates a dictionary from a name value collection by iterating over all of keys and values. The key will be case-insensitive.
        /// </summary>
        /// <param name="nameValueCollection"></param>
        /// <param name="equalityComparer"></param>
        /// <returns></returns>
        [NotNull]
        public static IDictionary<String, String> ToDictionary([NotNull] this NameValueCollection nameValueCollection, IEqualityComparer<String> equalityComparer = null)
        {
            equalityComparer = equalityComparer ?? StringComparer.CurrentCultureIgnoreCase;

            return nameValueCollection
                .Keys
                .Cast<String>()
                .Where(key => key != null)
                .ToDictionary(key => key, key => nameValueCollection[key], equalityComparer);
        }
    }
}
