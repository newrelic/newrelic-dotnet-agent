using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class DataReaderWrapper : IWrapper
	{
		public const string WrapperName = "DataReaderWrapper";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var isRequestedByName = WrapperName == methodInfo.RequestedWrapperName;

			var canWrap = isRequestedByName || method.MatchesAny(
				assemblyNames: new[]
				{
					"System.Data",
					"System.Data.SqlClient",
					"System.Data.OracleClient",
					"Oracle.DataAccess",
					"Oracle.ManagedDataAccess",
					"MySql.Data",
					"Npgsql",
					"IBM.Data.DB2"
				},
				typeNames: new[]
				{
					"System.Data.SqlClient.SqlDataReader",
					"System.Data.OracleClient.OracleDataReader",
					"Oracle.DataAccess.Client.OracleDataReader",
					"Oracle.ManagedDataAccess.Client.OracleDataReader",
					"MySql.Data.MySqlClient.MySqlDataReader",
					"Npgsql.ForwardsOnlyDataReader",
					"Npgsql.CachingDataReader",
					"IBM.Data.DB2.DB2DataReader"
				},
				methodNames: new[]
				{
					"NextResult",
					"Read"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "DatabaseResult/Iterate");
			segment.MakeCombinable();
				
			return Delegates.GetDelegateFor(segment);
		}
	}
}
