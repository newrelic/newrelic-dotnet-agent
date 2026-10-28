// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Providers.Wrapper.RabbitMq;

public sealed class RabbitMqConnectionIdentity
{
    public string ClusterName { get; }
    public string VirtualHost { get; }

    public RabbitMqConnectionIdentity(string clusterName, string virtualHost)
    {
        ClusterName = clusterName;
        VirtualHost = virtualHost;
    }
}
