using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers.Collections.Generic
{
    public static class IEnumerableExtensions
    {


        [NotNull, Pure]
        public static IEnumerable<T> ForEachLazy<T>([NotNull] this IEnumerable<T> source, Action<T> action)
        {
            return new ForEachEnumerable<T>(source, action);
        }

        public static void ForEachNow<T>([NotNull] this IEnumerable<T> source, Action<T> action)
        {
            var enumerable = source.ForEachLazy(action);
            // ReSharper disable once UnusedVariable
            foreach (var item in enumerable)
            {
                // do nothing, we just want to force enumeration
            }
        }
    }
}
