using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.TypeInstantiation
{
    public class TypeInstantiatorResult<T>
    {
        public readonly IEnumerable<T> Instances;
        public readonly IEnumerable<Exception> Exceptions;

        public TypeInstantiatorResult(IEnumerable<T> instances = null, IEnumerable<Exception> exceptions = null)
        {
            Instances = instances ?? Enumerable.Empty<T>();
            Exceptions = exceptions ?? Enumerable.Empty<Exception>();
        }
    }
}
