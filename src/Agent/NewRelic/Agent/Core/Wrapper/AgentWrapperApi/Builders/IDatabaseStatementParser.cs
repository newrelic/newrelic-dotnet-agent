using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    public interface IDatabaseStatementParser
    {
        ParsedSqlStatement ParseDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql);
    }
}
