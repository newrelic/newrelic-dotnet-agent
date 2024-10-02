// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic
{
    public class ForEachEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _enumerable;
        private readonly Action<T> _action;

        public ForEachEnumerable(IEnumerable<T> enumerable, Action<T> action)
        {
            _enumerable = enumerable;
            _action = action;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new ForEachEnumerator(_enumerable.GetEnumerator(), _action);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class ForEachEnumerator : IEnumerator<T>
        {
            private readonly IEnumerator<T> _enumerator;
            private readonly Action<T> _action;

            public ForEachEnumerator(IEnumerator<T> enumerator, Action<T> action)
            {
                _enumerator = enumerator;
                _action = action;
            }

            public T Current
            {
                get
                {
                    _action(_enumerator.Current);
                    return _enumerator.Current;
                }
            }

            public void Dispose()
            {
                _enumerator.Dispose();
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset()
            {
                _enumerator.Reset();
            }
        }
    }
}
