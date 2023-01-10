// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis2Plus
{
    public static class Common
    {
        public static ConnectionInfo GetConnectionInfoFromConnectionMultiplexer(IConnectionMultiplexer connectionMultiplexer, string utilizationHostName)
        {
            var endpoints = connectionMultiplexer.GetEndPoints();
            if (endpoints == null || endpoints.Length <= 0)
            {
                return null;
            }

            var endpoint = endpoints[0];

            var dnsEndpoint = endpoint as DnsEndPoint;
            var ipEndpoint = endpoint as IPEndPoint;

            string port = null;
            string host = null;

            if (dnsEndpoint != null)
            {
                port = dnsEndpoint.Port.ToString();
                host = ConnectionStringParserHelper.NormalizeHostname(dnsEndpoint.Host, utilizationHostName);
            }

            if (ipEndpoint != null)
            {
                port = ipEndpoint.Port.ToString();
                host = ConnectionStringParserHelper.NormalizeHostname(ipEndpoint.Address.ToString(), utilizationHostName);
            }

            if (host == null)
            {
                return null;
            }

            return new ConnectionInfo(host, port, null);
        }
    }
}
