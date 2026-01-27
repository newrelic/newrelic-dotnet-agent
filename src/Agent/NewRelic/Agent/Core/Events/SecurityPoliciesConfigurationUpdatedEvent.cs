// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Configuration;


namespace NewRelic.Agent.Core.Events;

public class SecurityPoliciesConfigurationUpdatedEvent
{
    public readonly SecurityPoliciesConfiguration Configuration;

    public SecurityPoliciesConfigurationUpdatedEvent(SecurityPoliciesConfiguration configuration)
    {
        Configuration = configuration;
    }
}