/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

namespace NewRelic.Providers.Storage.CallStack.AsyncLocal
{
    public class CallContextStorage<T> : CallContextStorageBase<T>
    {
        private readonly AsyncLocal<T> _storage;

        public CallContextStorage(string key)
        {
            _storage = new AsyncLocal<T>(key);
        }

        public override T GetData()
        {
            return _storage.Value;
        }

        public override void SetData(T value)
        {
            _storage.Value = value;
        }

        public override void Clear()
        {
            _storage.Value = default;
        }
    }
}
