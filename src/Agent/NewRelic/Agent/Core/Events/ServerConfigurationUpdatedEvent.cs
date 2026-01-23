// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Configuration;

namespace NewRelic.Agent.Core.Events;

public class ServerConfigurationUpdatedEvent
{
    public readonly ServerConfiguration Configuration;

    public ServerConfigurationUpdatedEvent(ServerConfiguration configuration)
    {
        Configuration = configuration;
    }
}