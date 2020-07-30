/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.Parsing.ConnectionString;

namespace NewRelic.Providers.Wrapper.Sql
{
    public class SqlCommandWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(
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

            var sql = sqlCommand.CommandText ?? string.Empty;
            var vendor = SqlWrapperHelper.GetVendorName(sqlCommand);
            object GetConnectionInfo() => ConnectionInfo.FromConnectionString(vendor, sqlCommand.Connection.ConnectionString);
            var connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(sqlCommand.Connection.ConnectionString, GetConnectionInfo);

            var parsedStatement = transaction.GetParsedDatabaseStatement(sqlCommand.CommandType, sql);
            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement?.Operation, vendor, parsedStatement?.Model, sql,
                host: connectionInfo.Host, portPathOrId: connectionInfo.PortPathOrId, databaseName: connectionInfo.DatabaseName);

            if (vendor == DatastoreVendor.MSSQL)
            {
                agentWrapperApi.EnableExplainPlans(segment, () => SqlServerExplainPlanActions.AllocateResources(sqlCommand), SqlServerExplainPlanActions.GenerateExplainPlan, null);
            }
            else if (vendor == DatastoreVendor.MySQL)
            {
                if (parsedStatement != null)
                {
                    agentWrapperApi.EnableExplainPlans(segment, () => MySqlExplainPlanActions.AllocateResources(sqlCommand), MySqlExplainPlanActions.GenerateExplainPlan, () => MySqlExplainPlanActions.ShouldGenerateExplainPlan(sql, parsedStatement));
                }
            }

            return Delegates.GetDelegateFor(segment);
        }

    }
}
