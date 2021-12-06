// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET462

using System;
using System.Configuration;
using NServiceBus.Config;
using NServiceBus.Config.ConfigurationSource;

public class ConfigurationSource : IConfigurationSource
{
    public T GetConfiguration<T>() where T : class, new()
    {
        if (typeof(T) == typeof(MessageForwardingInCaseOfFaultConfig))
        {
            var config = new MessageForwardingInCaseOfFaultConfig
            {
                ErrorQueue = "error"
            };

            return config as T;
        }

        if (typeof(T) == typeof(TransportConfig))
        {
            var config = new TransportConfig
            {
                MaxRetries = 0
            };

            return config as T;
        }

        if (typeof(T) == typeof(SecondLevelRetriesConfig))
        {
            var config = new SecondLevelRetriesConfig
            {
                Enabled = false,
                NumberOfRetries = 0,
                TimeIncrease = TimeSpan.FromSeconds(10)
            };

            return config as T;
        }

        if (typeof(T) == typeof(AuditConfig))
        {
            var config = new AuditConfig
            {
                QueueName = "audit"
            };

            return config as T;
        }

        // Respect app.config for other sections not defined in this method
        return ConfigurationManager.GetSection(typeof(T).Name) as T;
    }
}

#endif
