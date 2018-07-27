#if NET45
using System;
using System.Data.OleDb;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class OleDbCommandWrapper : IWrapper
	{
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "System.Data", typeName: "System.Data.OleDb.OleDbCommand",
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
				var oleDbCommand = (OleDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
				if (oleDbCommand == null)
					return Delegates.NoOp;

				var sql = oleDbCommand.CommandText ?? String.Empty;
				var vendor = SqlWrapperHelper.GetVendorName(oleDbCommand);

				// TODO - Tracer had a supportability metric here to report timing duration of the parser.
				var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, oleDbCommand.CommandType, sql);

				var queryParameters = SqlWrapperHelper.GetQueryParameters(oleDbCommand, agentWrapperApi);

				var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, null, sql, queryParameters);

				return Delegates.GetDelegateFor(segment);
			}
		}
	}
}
#endif