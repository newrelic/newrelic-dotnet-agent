﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Providers.Wrapper.WrapperUtilities;

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
				return WrapperUtils.LegacyAspPipelineIsPresent()
					? new CanWrapResponse(false, WrapperUtils.LegacyAspPipelineNotSupportedMessage("Npgsql", "Npgsql.NpgsqlCommand", method.MethodName))
					: new CanWrapResponse(true);

			}

			return new CanWrapResponse(false);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var sqlCommand = (IDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (sqlCommand == null)
				return Delegates.NoOp;

			var sql = sqlCommand.CommandText ?? String.Empty;

			// NOTE: this wrapper currently only supports NpgsqlCommand. If support for other commands is added to this wrapper then the vendor will need to be determined dynamically.
			const DatastoreVendor vendor = DatastoreVendor.Postgres;
			object GetConnectionInfo() => ConnectionInfo.FromConnectionString(vendor, sqlCommand.Connection.ConnectionString);
			var connectionInfo = (ConnectionInfo) transaction.GetOrSetValueFromCache(sqlCommand.Connection.ConnectionString, GetConnectionInfo);

			// TODO - Tracer had a supportability metric here to report timing duration of the parser.
			var parsedStatement = transaction.GetParsedDatabaseStatement(sqlCommand.CommandType, sql);
			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement?.Operation, vendor, parsedStatement?.Model, sql,
				host: connectionInfo.Host, portPathOrId: connectionInfo.PortPathOrId, databaseName: connectionInfo.DatabaseName);

			return WrapperUtils.GetAsyncDelegateFor(agentWrapperApi, segment);
		}
	}
}
