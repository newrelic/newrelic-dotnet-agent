/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// This exception is thrown when the Agent is to restart, as for example
    /// when Agent settings on the RPM change.
    /// </summary>
    public class ForceRestartException : InstructionException
    {

        public ForceRestartException(string message) : base(message)
        {
        }
    }
}
