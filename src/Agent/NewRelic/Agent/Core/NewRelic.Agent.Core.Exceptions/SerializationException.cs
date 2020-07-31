// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Exceptions
{
    public class SerializationException : RPMException
    {
        public SerializationException(string message)
            : base(message)
        {
        }
    }
}
