/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.Exceptions
{
    public class RuntimeException : RPMException
    {
        public RuntimeException(string message)
            : base(message)
        {
        }

        public RuntimeException(string message, Exception ex)
            : base(message, ex)
        {
        }
    }
}
