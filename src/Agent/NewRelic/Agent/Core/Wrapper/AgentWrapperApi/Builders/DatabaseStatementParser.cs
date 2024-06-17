// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Data;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    public class DatabaseStatementParser : ConfigurationBasedService, IDatabaseStatementParser
    {
        private CacheByDatastoreVendor<string, ParsedSqlStatement> _cache;

        public DatabaseStatementParser()
        {
            _cache = new CacheByDatastoreVendor<string, ParsedSqlStatement>("SqlParsingCache");
        }

        public ParsedSqlStatement ParseDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql)
        {
            switch (commandType)
            {
                case CommandType.TableDirect:
                case CommandType.StoredProcedure:
                    return SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql);
                default:
                    return _cache.GetOrAdd(datastoreVendor, sql, () => SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql));
            }
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            _cache.SetCapacity(_configuration.DatabaseStatementCacheCapacity);
        }
    }
}
