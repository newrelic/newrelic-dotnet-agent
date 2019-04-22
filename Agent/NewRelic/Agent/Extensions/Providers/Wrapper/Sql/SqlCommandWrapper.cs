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
		public const string NpgsqlWrapperName = "NpgsqlSqlCommandWrapper";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;

			var isRequestedByName = 
				(methodInfo.RequestedWrapperName == WrapperName) ||
				(methodInfo.RequestedWrapperName == NpgsqlWrapperName);
			
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

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
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

			var queryParameters = SqlWrapperHelper.GetQueryParameters(sqlCommand, agent);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, connectionInfo, sql, queryParameters, isLeaf: true);

			if (vendor == DatastoreVendor.MSSQL)
			{
				agent.EnableExplainPlans(segment, () => SqlServerExplainPlanActions.AllocateResources(sqlCommand), SqlServerExplainPlanActions.GenerateExplainPlan, null);
			}
			else if (vendor == DatastoreVendor.MySQL)
			{
				if (parsedStatement != null)
				{
					agent.EnableExplainPlans(segment, () => MySqlExplainPlanActions.AllocateResources(sqlCommand), MySqlExplainPlanActions.GenerateExplainPlan, () => MySqlExplainPlanActions.ShouldGenerateExplainPlan(sql, parsedStatement));
				}
			}

			return Delegates.GetDelegateFor(segment);
		}

	}
}
