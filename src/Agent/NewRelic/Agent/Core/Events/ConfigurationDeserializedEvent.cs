// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Events;

public class ConfigurationDeserializedEvent
{
    public Config.configuration Configuration;

    public ConfigurationDeserializedEvent(Config.configuration configuration)
    {
        Configuration = configuration;
    }
}