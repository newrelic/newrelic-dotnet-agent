/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.Exceptions
{
    /// <summary>
    /// An exception thrown from the RPM service to tell the agent to do something like shutdown or restart.
    /// </summary>
    public class InstructionException : RPMException
    {
        public InstructionException(string message) : base(message)
        {
        }

        public InstructionException(string message, Exception exception) : base(message, exception)
        {
        }
    }
}
