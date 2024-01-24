// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Database
{
    public interface IDatabaseService : IDisposable
    {
        long GetSqlId(string sql, DatastoreVendor vendor);
        string GetObfuscatedSql(string sql, DatastoreVendor vendor);
    }

    public class DatabaseService : ConfigurationBasedService, IDatabaseService
    {
        private SqlObfuscator _sqlObfuscator;
        private readonly CacheByDatastoreVendor<string, string> _cache;

        public DatabaseService()
        {
            _sqlObfuscator = SqlObfuscator.GetSqlObfuscator(_configuration.TransactionTracerRecordSql);
            _cache = new CacheByDatastoreVendor<string, string>("SqlObfuscationCache");
        }

        /// <summary>
        /// The generation of SQL IDs always uses obfuscation in order to normalize the SQL and generate the same ID for queries that differ only in their parameterization.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public long GetSqlId(string sql, DatastoreVendor vendor)
        {
            var obfuscatedSql = GetObfuscatedSqlFromCache(sql, vendor);
            return GenerateSqlId(obfuscatedSql);
        }

        /// <summary>
        /// If the SQL obfuscation settings are set to obfuscate, this will return the obfuscated SQL using the cache. Otherwise, it just returns
        /// the value returned from the SQL obfuscator defined by the configuration because there is no need to cache the value of the no sql and raw sql obfuscators.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public string GetObfuscatedSql(string sql, DatastoreVendor vendor)
        {
            return _sqlObfuscator != SqlObfuscator.GetObfuscatingSqlObfuscator()
                ? _sqlObfuscator.GetObfuscatedSql(sql, vendor)
                : GetObfuscatedSqlFromCache(sql, vendor);
        }

        /// <summary>
        /// SQL obfuscation is expensive. It is performed when a transaction has ended in the creation of SQL traces and transaction traces.
        /// Whether we obfuscate SQL for traces depends on configuration. We reduce the number of times SQL obfuscation is performed by caching
        /// results.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private string GetObfuscatedSqlFromCache(string sql, DatastoreVendor vendor)
        {
            return _cache.GetOrAdd(vendor, sql, ObfuscateSql);

            string ObfuscateSql()
            {
                return SqlObfuscator.GetObfuscatingSqlObfuscator().GetObfuscatedSql(sql, vendor);
            }
        }

        private long GenerateSqlId(string sql)
        {
            var hashCode = string.IsNullOrEmpty(sql) ? string.Empty.GetHashCode() : sql.GetHashCode();
            var numberOfDigits = Math.Floor(Math.Log10(hashCode) + 1);

            if (numberOfDigits == 9)
            {
                hashCode = hashCode % 100000000;    // reduce to 8 digits
            }

            return hashCode;
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).

            _sqlObfuscator = SqlObfuscator.GetSqlObfuscator(_configuration.TransactionTracerRecordSql);
            _cache.SetCapacity(_configuration.DatabaseStatementCacheCapacity);
        }
    }
}
