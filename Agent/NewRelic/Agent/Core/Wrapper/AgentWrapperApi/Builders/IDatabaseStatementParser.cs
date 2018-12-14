using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public interface IDatabaseStatementParser
	{
		uint CacheCapacity { get; set; }
		int GetCacheSize(DatastoreVendor datastoreVendor);
		int GetCacheHits(DatastoreVendor datastoreVendor);
		int GetCacheMisses(DatastoreVendor datastoreVendor);
		int GetCacheEjections(DatastoreVendor datastoreVendor);
		void ResetStats();
		void ResetCaches();
		ParsedSqlStatement ParseDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql);
	}
}