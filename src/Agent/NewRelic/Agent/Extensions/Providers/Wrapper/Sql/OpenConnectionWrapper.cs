using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Sql
{
    public class OpenConnectionWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyNames: new[]
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
                    "System.Data.SqlClient.SqlConnection",
                    "System.Data.Odbc.OdbcConnection",
                    "System.Data.OleDb.OleDbConnection",
                    "System.Data.OracleClient.OracleConnection",
                    "Oracle.DataAccess.Client.OracleConnection",
                    "Oracle.ManagedDataAccess.Client.OracleConnection",
                    "MySql.Data.MySqlClient.MySqlConnection",
                    "Npgsql.NpgsqlConnection",
                    "IBM.Data.DB2.DB2Connection"
                },
                methodNames: new[]
                {
                    "Open"
                });
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "unknown";
            var segment = transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, instrumentedMethodCall.MethodCall.Method.MethodName);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
