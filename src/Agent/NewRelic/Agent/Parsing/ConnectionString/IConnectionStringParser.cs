// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Caching;

namespace NewRelic.Parsing.ConnectionString
{
    public interface IConnectionStringParser
    {
        ConnectionInfo GetConnectionInfo(string utilizationHostName);
    }

    public static class ConnectionInfoParser
    {
        private const int CacheCapacity = 1000;
        private static readonly SimpleCache<string, ConnectionInfo> _connectionInfoCache = new SimpleCache<string, ConnectionInfo>(CacheCapacity);

        private static readonly ConnectionInfo Empty = new ConnectionInfo(null, null, null, null);

        public static ConnectionInfo FromConnectionString(DatastoreVendor vendor, string connectionString, string utilizationHostName)
        {
            return _connectionInfoCache.GetOrAdd(connectionString, () =>
            {
                IConnectionStringParser parser = GetConnectionParser(vendor, connectionString);
                return parser?.GetConnectionInfo(utilizationHostName) ?? Empty;
            });
        }

        private static IConnectionStringParser GetConnectionParser(DatastoreVendor vendor, string connectionString)
        {
            switch (vendor)
            {
                case DatastoreVendor.MSSQL:
                    return new MsSqlConnectionStringParser(connectionString);
                case DatastoreVendor.MySQL:
                    return new MySqlConnectionStringParser(connectionString);
                case DatastoreVendor.Postgres:
                    return new PostgresConnectionStringParser(connectionString);
                case DatastoreVendor.Oracle:
                    return new OracleConnectionStringParser(connectionString);
                case DatastoreVendor.IBMDB2:
                    return new IbmDb2ConnectionStringParser(connectionString);
                case DatastoreVendor.Redis:
                    return new StackExchangeRedisConnectionStringParser(connectionString);
                default:
                    return null;
            }
        }
    }
}
