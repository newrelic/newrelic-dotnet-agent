/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.Core.Exceptions
{
    /// <summary>
    /// This exception is thrown when there is a problem with the license key, as reported by the collector(RPM).
    /// </summary>
    public class LicenseException : RPMException
    {
        public LicenseException(string message) : base(message)
        {
        }

        public LicenseException(string message, Exception exception)
            : base(message, exception)
        {
        }
    }
}
