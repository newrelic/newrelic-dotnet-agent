// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core
{
    [Serializable]
    public class SimpleException : Exception
    {
        public SimpleException(string exceptionName)
            : base(exceptionName)
        {
        }
        public SimpleException(string exceptionName, Exception ex)
            : base(exceptionName, ex)
        {
        }
    }
}
