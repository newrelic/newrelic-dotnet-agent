using System;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Collections;
using NewRelic.Parsing;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	internal class DatabaseStatementParser
	{
		private readonly ConcurrentDictionary<CommandType, ConcurrentDictionary<string, ParsedSqlStatement>> _typeToStatementCache =
			new ConcurrentDictionary<CommandType, ConcurrentDictionary<string, ParsedSqlStatement>>(3);

		internal ParsedSqlStatement ParseDatabaseStatement(CommandType commandType, string sql)
		{
			var sqlToStatement = _typeToStatementCache.GetOrSetValue(commandType, () => new ConcurrentDictionary<string, ParsedSqlStatement>());
			var cachedStatement = sqlToStatement.GetOrSetValue(sql, () => SqlParser.GetParsedDatabaseStatement(commandType, sql));
			return SqlParser.NullStatement == cachedStatement ? null : cachedStatement;
		}
	}
}
