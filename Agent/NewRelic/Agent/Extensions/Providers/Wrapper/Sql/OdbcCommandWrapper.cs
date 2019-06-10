#if NET45
using System;
using System.Data.Odbc;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class OdbcCommandWrapper : IWrapper
	{
		public const string WrapperName = "OdbcCommandTracer";
		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse(methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			{
				var odbcCommand = (OdbcCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
				if (odbcCommand == null)
					return Delegates.NoOp;

				var sql = odbcCommand.CommandText ?? String.Empty;
				var vendor = SqlWrapperHelper.GetVendorName(odbcCommand);

				// TODO - Tracer had a supportability metric here to report timing duration of the parser.
				var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, odbcCommand.CommandType, sql);

				var queryParameters = SqlWrapperHelper.GetQueryParameters(odbcCommand, agent);

				var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, null, sql, queryParameters);

				return Delegates.GetDelegateFor(segment);
			}
		}
	}
}
#endif