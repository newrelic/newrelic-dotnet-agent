#if NET45
using System;
using System.Data.Odbc;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class OdbcCommandWrapper :IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Data", typeName: "System.Data.Odbc.OdbcCommand",
				methodNames: new[]
				{
					"ExecuteReader",
					"ExecuteNonQuery",
					"ExecuteScalar"
				});
			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			{
				var odbcCommand = (OdbcCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
				if (odbcCommand == null)
					return Delegates.NoOp;

				var sql = odbcCommand.CommandText ?? String.Empty;
				var vendor = SqlWrapperHelper.GetVendorName(odbcCommand);

				// TODO - Tracer had a supportability metric here to report timing duration of the parser.
				var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, odbcCommand.CommandType, sql);

				var queryParameters = SqlWrapperHelper.GetQueryParameters(odbcCommand, agentWrapperApi);

				var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, null, sql, queryParameters);

				return Delegates.GetDelegateFor(segment);
			}
		}
	}
}
#endif