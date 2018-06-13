using System;
using System.Collections.Concurrent;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class DatabaseStatementParser
	{
		/// <summary>
		/// An array of caches of sql to parsed sql statements.  The index of the array is
		/// the DatabaseVendor.
		/// </summary>
		private readonly ConcurrentDictionary<string, ParsedSqlStatement>[] _vendorToStatementCache;

		public DatabaseStatementParser()
		{
			var vendorCount = Enum.GetValues(typeof(DatastoreVendor)).Length;
			_vendorToStatementCache = new ConcurrentDictionary<string, ParsedSqlStatement>[vendorCount];
		}

		public ParsedSqlStatement ParseDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql)
		{
			switch (commandType)
			{
				case CommandType.TableDirect:
				case CommandType.StoredProcedure:
					return SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql);
				default:
					var sqlToStatement = GetOrCreateSqlCache(datastoreVendor);
					var cachedStatement = sqlToStatement.GetOrAdd(sql, key => SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql));
					return cachedStatement;
			}
		}

		private ConcurrentDictionary<string, ParsedSqlStatement> GetOrCreateSqlCache(DatastoreVendor datastoreVendor)
		{
			var vendorIndex = (int)datastoreVendor;
			var sqlToStatement = Interlocked.CompareExchange(ref _vendorToStatementCache[vendorIndex], null, null);
			if (null == sqlToStatement)
			{
				Interlocked.CompareExchange(ref _vendorToStatementCache[vendorIndex], new ConcurrentDictionary<string, ParsedSqlStatement>(), null);
			}
			else
			{
				return sqlToStatement;
			}
			return Interlocked.CompareExchange(ref _vendorToStatementCache[vendorIndex], null, null);
		}
	}
}
