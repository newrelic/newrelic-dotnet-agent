using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.SqlAsync
{
	public class PostgresCommandWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(
				assemblyNames: new[]
				{
					"Npgsql"
				},
				typeNames: new[]
				{
					"Npgsql.NpgsqlCommand"
				},
				methodNames: new[]
				{
					"ExecuteAsync",
					"Execute" 
				});

			if (canWrap)
			{
				return TaskFriendlySyncContextValidator.CanWrapAsyncMethod("Npgsql", "Npgsql.NpgsqlCommand", method.MethodName);

			}

			return new CanWrapResponse(false);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transactionWrapperApi.AttachToAsync();
			}

			var sqlCommand = (IDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (sqlCommand == null)
				return Delegates.NoOp;

			var sql = sqlCommand.CommandText ?? String.Empty;

			// NOTE: this wrapper currently only supports NpgsqlCommand. If support for other commands is added to this wrapper then the vendor will need to be determined dynamically.
			const DatastoreVendor vendor = DatastoreVendor.Postgres;
			object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(vendor, sqlCommand.Connection.ConnectionString);
			var connectionInfo = (ConnectionInfo) transactionWrapperApi.GetOrSetValueFromCache(sqlCommand.Connection.ConnectionString, GetConnectionInfo);

			// TODO - Tracer had a supportability metric here to report timing duration of the parser.
			var parsedStatement = transactionWrapperApi.GetParsedDatabaseStatement(vendor, sqlCommand.CommandType, sql);

			var queryParameters = SqlWrapperHelper.GetQueryParameters(sqlCommand, agentWrapperApi);

			var segment = transactionWrapperApi.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
				parsedStatement, connectionInfo, sql, queryParameters);

			return Delegates.GetAsyncDelegateFor(agentWrapperApi, segment);
		}
	}
}
