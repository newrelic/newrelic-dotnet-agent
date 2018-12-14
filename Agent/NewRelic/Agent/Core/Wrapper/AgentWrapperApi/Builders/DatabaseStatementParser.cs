using System;
using System.Collections.Concurrent;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading;
using NewRelic.Core.NewRelic.Cache;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class DatabaseStatementParser : ConfigurationBasedService, IDatabaseStatementParser
	{
		public DatabaseStatementParser()
		{
			for (var i = 0; i < Enum.GetValues(typeof(DatastoreVendor)).Length; i++)
			{
				VendorToStatementCache[i] = new SimpleCache<string, ParsedSqlStatement>(CacheCapacity);
			}
		}

		protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
		{
			if(_configuration.DatabaseStatementCacheCapcity != CacheCapacity)
			{
				var oldCapacity = CacheCapacity;
				CacheCapacity = _configuration.DatabaseStatementCacheCapcity;

				Log.Info($"The SQL statement cache capacity has been modified from {oldCapacity} to {CacheCapacity}. Agent's memory allocation will be affected by this change so use with precaution.");
			}
		}

		/// <summary>
		/// An array of caches of sql to parsed sql statements.  The index of the array is
		/// the DatabaseVendor.
		/// </summary>
		private readonly SimpleCache<string, ParsedSqlStatement>[] VendorToStatementCache =
			new SimpleCache<string, ParsedSqlStatement>[Enum.GetValues(typeof(DatastoreVendor)).Length];

		// The MaxCacheSize is set at startup and can be configured from the SqlStatementCacheMaxSize setting in the local newrelic.config 
		private uint _cacheCapacity = 1000;

		public uint CacheCapacity
		{
			get => _cacheCapacity;
			set
			{
				_cacheCapacity = value;
				for (var i = 0; i < Enum.GetValues(typeof(DatastoreVendor)).Length; i++)
				{
					VendorToStatementCache[i].Capacity = value;
				}
			}
		}

		public int GetCacheSize(DatastoreVendor datastoreVendor)
		{
			return VendorToStatementCache[(int) datastoreVendor].Size;
		}

		public int GetCacheHits(DatastoreVendor datastoreVendor)
		{
			return VendorToStatementCache[(int)datastoreVendor].CountHits;
		}

		public int GetCacheMisses(DatastoreVendor datastoreVendor)
		{
			return VendorToStatementCache[(int)datastoreVendor].CountMisses;
		}

		public int GetCacheEjections(DatastoreVendor datastoreVendor)
		{
			return VendorToStatementCache[(int)datastoreVendor].CountEjections;
		}

		public void ResetStats()
		{
			foreach (var cache in VendorToStatementCache)
			{
				cache.ResetStats();
			}
		}

		//mainly uses for testing purposes.
		public void ResetCaches()
		{
			foreach (var stmt in VendorToStatementCache)
			{
				stmt.Reset();
			}
		}

		public ParsedSqlStatement ParseDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql)
		{
			switch (commandType)
			{
				case CommandType.TableDirect:
				case CommandType.StoredProcedure:
					return SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql);
				default:
					var sqlToStatement = VendorToStatementCache[(int)datastoreVendor];
					var cachedStatement = sqlToStatement.GetOrAdd(sql, () => SqlParser.GetParsedDatabaseStatement(datastoreVendor, commandType, sql));
					return cachedStatement;
			}
		}
	}
}
