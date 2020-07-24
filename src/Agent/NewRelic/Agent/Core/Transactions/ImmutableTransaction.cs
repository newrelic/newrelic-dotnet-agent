using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Transactions
{
    public class ImmutableTransaction
    {
        [NotNull]
        public readonly ITransactionName TransactionName;

        [NotNull]
        public readonly IEnumerable<Segment> Segments;

        [NotNull]
        public readonly ImmutableTransactionMetadata TransactionMetadata;

        public readonly DateTime StartTime;
        public readonly TimeSpan Duration;

        [NotNull]
        public readonly String Guid;

        public readonly Boolean IgnoreAutoBrowserMonitoring;
        public readonly Boolean IgnoreAllBrowserMonitoring;
        public readonly Boolean IgnoreApdex;

        /// <summary>
        /// The SQL obfuscator as defined by user configuration
        /// </summary>
        private readonly SqlObfuscator _sqlObfuscator;
        private IDictionary<string, string> _obfuscatedSqlCache;
        private IDictionary<string, string> ObfuscatedSqlCache => _obfuscatedSqlCache ?? (_obfuscatedSqlCache = new Dictionary<string, string>());

        // The sqlObfuscator parameter should be the SQL obfuscator as defined by user configuration: obfuscate, off, or raw.
        public ImmutableTransaction([NotNull] ITransactionName transactionName, [NotNull] IEnumerable<Segment> segments, [NotNull] ImmutableTransactionMetadata transactionMetadata, DateTime startTime, TimeSpan duration, [NotNull] string guid, bool ignoreAutoBrowserMonitoring, bool ignoreAllBrowserMonitoring, bool ignoreApdex, SqlObfuscator sqlObfuscator)
        {
            TransactionName = transactionName;
            Segments = segments.Where(segment => segment != null).ToList();
            TransactionMetadata = transactionMetadata;
            StartTime = startTime;
            Duration = duration;
            Guid = guid;
            IgnoreAutoBrowserMonitoring = ignoreAutoBrowserMonitoring;
            IgnoreAllBrowserMonitoring = ignoreAllBrowserMonitoring;
            IgnoreApdex = ignoreApdex;

            _sqlObfuscator = sqlObfuscator;
        }

        public Boolean IsWebTransaction()
        {
            return TransactionName.IsWeb;
        }

        /// <summary>
        /// The generation of SQL IDs always uses obfuscation in order to normalize the SQL and generate the same ID for queries that differ only in their parameterization.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public long GetSqlId(string sql, DatastoreVendor vendor)
        {
            var obfuscatedSql = GetObfuscatedSqlFromCache(sql, vendor);
            return DatabaseService.GenerateSqlId(obfuscatedSql);
        }

        /// <summary>
        /// If the SQL obfuscation settings are set to obfuscate, this will return the obfuscated SQL using the cache. Otherwise, it just returns
        /// the value returned from the SQL obfuscator defined by the configuration because there is no need to cache the value of the no sql and raw sql obfuscators.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public string GetSqlObfuscatedAccordingToConfig(string sql, DatastoreVendor vendor)
        {
            return _sqlObfuscator != SqlObfuscator.GetObfuscatingSqlObfuscator() ? _sqlObfuscator.GetObfuscatedSql(sql, vendor) : GetObfuscatedSqlFromCache(sql, vendor);
        }

        /// <summary>
        /// SQL obfuscation is expensive. It is performed when a transaction has ended in the creation of SQL traces and transaction traces.
        /// Whether we obfuscate SQL for traces depends on configuration. We reduce the number of times SQL obfuscation is performed by caching
        /// results on the transaction.
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private string GetObfuscatedSqlFromCache(string sql, DatastoreVendor vendor)
        {
            if (!ObfuscatedSqlCache.TryGetValue(sql, out var obfuscatedSql))
            {
                obfuscatedSql = SqlObfuscator.GetObfuscatingSqlObfuscator().GetObfuscatedSql(sql, vendor);
                ObfuscatedSqlCache[sql] = obfuscatedSql;
            }

            return obfuscatedSql;
        }
    }
}
