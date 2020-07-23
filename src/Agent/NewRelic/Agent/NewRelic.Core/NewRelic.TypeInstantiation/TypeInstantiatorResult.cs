using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.TypeInstantiation
{
    public class TypeInstantiatorResult<T>
    {
        [NotNull]
        public readonly IEnumerable<T> Instances;

        [NotNull]
        public readonly IEnumerable<Exception> Exceptions;

        public TypeInstantiatorResult([CanBeNull] IEnumerable<T> instances = null, [CanBeNull] IEnumerable<Exception> exceptions = null)
        {
            Instances = instances ?? Enumerable.Empty<T>();
            Exceptions = exceptions ?? Enumerable.Empty<Exception>();
        }
    }
}
