using System;
using System.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class SqlCommandWrapper : IWrapper
	{
		public const string WrapperName = "SqlCommandWrapper";

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
					"Devart.Data.MySql",
					"Npgsql",
					"IBM.Data.DB2"
				},
				typeNames: new[]
				{
					"System.Data.SqlClient.SqlCommand",
					"System.Data.OracleClient.OracleCommand",
					"Oracle.DataAccess.Client.OracleCommand",
					"Oracle.ManagedDataAccess.Client.OracleCommand",
					"MySql.Data.MySqlClient.MySqlCommand",
					"Devart.Data.MySql.MySqlCommand",
					"Npgsql.NpgsqlCommand",
					"IBM.Data.DB2.DB2Command"
				},
				methodNames: new[]
				{
					"ExecuteReader",
					"ExecuteNonQuery",
					"ExecuteScalar",
					"ExecuteXmlReader"
				});

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var sqlCommand = (IDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
			if (sqlCommand == null)
				return Delegates.NoOp;

			var sql = sqlCommand.CommandText ?? String.Empty;
			var vendor = SqlWrapperHelper.GetVendorName(sqlCommand);
			object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(vendor, sqlCommand.Connection.ConnectionString);
			var connectionInfo = (ConnectionInfo) transaction.GetOrSetValueFromCache(sqlCommand.Connection.ConnectionString, GetConnectionInfo);

			// TODO - Tracer had a supportability metric here to report timing duration of the parser.
			var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, sqlCommand.CommandType, sql);
			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, connectionInfo, sql);

			if (vendor == DatastoreVendor.MSSQL)
			{
				agentWrapperApi.EnableExplainPlans(segment, () => SqlServerExplainPlanActions.AllocateResources(sqlCommand), SqlServerExplainPlanActions.GenerateExplainPlan);
			}
			else if (vendor == DatastoreVendor.MySQL)
			{
				if (parsedStatement != null && parsedStatement.Operation.Equals("select", StringComparison.CurrentCultureIgnoreCase))
				{
					agentWrapperApi.EnableExplainPlans(segment, () => MySqlExplainPlanActions.AllocateResources(sqlCommand), MySqlExplainPlanActions.GenerateExplainPlan);
				}
			}

			return Delegates.GetDelegateFor(segment);
		}
	}
}
