// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;

namespace NewRelic.Agent.Extensions.Helpers;

public static class RabbitMqServerProperties
{
    private const string ClusterNameKey = "cluster_name";

    public static string GetClusterName(IDictionary<string, object> serverProperties)
    {
        if (serverProperties == null || !serverProperties.TryGetValue(ClusterNameKey, out var value))
        {
            return null;
        }

        var clusterName = value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => null
        };

        return string.IsNullOrEmpty(clusterName) ? null : clusterName;
    }
}
