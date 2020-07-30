/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Database
{
    public interface IDatabaseService
    {
        SqlObfuscator SqlObfuscator { get; }
    }
    public class DatabaseService : ConfigurationBasedService, IDatabaseService
    {
        public SqlObfuscator SqlObfuscator { get; private set; }

        public DatabaseService()
        {
            SqlObfuscator = SqlObfuscator.GetSqlObfuscator(_configuration.TransactionTracerEnabled, _configuration.TransactionTracerRecordSql);
        }

        public static long GenerateSqlId(string sql)
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

            SqlObfuscator = SqlObfuscator.GetSqlObfuscator(_configuration.TransactionTracerEnabled, _configuration.TransactionTracerRecordSql);
        }
    }
}
