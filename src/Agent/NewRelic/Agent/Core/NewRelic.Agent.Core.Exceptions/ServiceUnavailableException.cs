// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Exceptions
{
    /// <summary>
    /// This exception is thrown when the agent receives a service unavailable error
    /// </summary>
    public class ServiceUnavailableException : Exception
    {
        public ServiceUnavailableException(string message)
            : base(message)
        {
        }
    }
}
