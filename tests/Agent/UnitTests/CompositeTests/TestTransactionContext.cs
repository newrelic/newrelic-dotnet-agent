// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers;
using NewRelic.SystemExtensions.Collections.Generic;

namespace CompositeTests
{
    public class TestTransactionContext<T> : IContextStorage<T>
    {
        private readonly string _key = "TEST";

        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public T GetData()
        {
            return (T)_data.GetValueOrDefault(_key);
        }

        public void SetData(T value)
        {
            _data[_key] = value;
        }

        public void Clear()
        {
            _data.Remove(_key);
        }

        public byte Priority
        {
            get { return 10; }
        }

        public bool CanProvide
        {
            get { return true; }
        }
    }
}
