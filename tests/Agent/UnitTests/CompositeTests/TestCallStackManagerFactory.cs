// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.CallStack;

namespace CompositeTests
{
    internal class TestCallStackManagerFactory : ICallStackManagerFactory, ICallStackManager
    {
        private int? _parent;

        public bool AttachToAsync()
        {
            return true;
        }

        public void Clear()
        {
            _parent = null;
        }

        public ICallStackManager CreateCallStackManager()
        {
            return this;
        }

        public void Push(int uniqueId)
        {
            _parent = uniqueId;
        }

        public int? TryPeek()
        {
            return _parent;
        }

        public void TryPop(int uniqueId, int? parentId)
        {
            _parent = parentId;
        }
    }
}
