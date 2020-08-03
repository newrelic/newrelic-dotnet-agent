// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Exceptions
{

    /// <summary>
    /// Thrown on a disconnect between the collector(RPM) and the Agent
    /// </summary>
    public class DisconnectedException : RPMException
    {
        public DisconnectedException(string message) : base(message)
        {
        }
    }
}
