// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// This exception is thrown when there has been a disconnection between the collector(RPM) and the Agent.
    /// </summary>
    public class ForceDisconnectException : InstructionException
    {
        public ForceDisconnectException(string message) : base(message)
        {
        }
    }
}
