/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// A base clase for specific exceptions that appear to be thrown from the collector/RPM back to the Agent.
    /// </summary>
    public class RPMException : System.Exception
    {
        public RPMException(string message) : base(message)
        {
        }

        public RPMException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}
