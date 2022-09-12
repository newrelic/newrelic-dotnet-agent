// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent
{
    internal class NoOpSpan : ISpan
    {
        public ISpan AddCustomAttribute(string key, object value)
        {
            return this;
        }

        public ISpan SetName(string name)
        {
            return this;
        }
    }
}
