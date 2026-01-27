// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Configuration;

public class SecurityPoliciesValidationException : Exception
{
    public SecurityPoliciesValidationException()
    {
    }

    public SecurityPoliciesValidationException(string message) : base(message)
    {
    }

    public SecurityPoliciesValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
