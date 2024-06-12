// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Parsing.ConnectionString;
using System.Linq;
using NewRelic.Agent.Api;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.Sql
{
    public class SqlCommandAsyncWrapper : SqlCommandWrapperBase
    {
        private static readonly string[] _tracerNames =
        {
            "SqlCommandTracerAsync",
            "SqlCommandWrapperAsync"
        };

        public override string[] WrapperNames => _tracerNames;

        public override bool ExecuteAsAsync => true;
    }

    public class SqlCommandWrapper : SqlCommandWrapperBase
    {

        private static readonly string[] _tracerNames =
        {
            "SqlCommandTracer",
            "SqlCommandWrapper"
        };

        public override string[] WrapperNames => _tracerNames;

        public override bool ExecuteAsAsync => false;
    }

    public abstract class SqlCommandWrapperBase : IWrapper
    {
        public abstract string[] WrapperNames { get; }

        /// <summary>
        /// Sometimes, the methods that are being instrumented appear to be async in that they return a Task, but they are not actually
        /// decorated with the async decorator.  When this happens, the InstrumentedMethodCall.IsAsync cannot be relied upon to determine 
        /// whether or not to attach the AfterWrappedMethod as a continuation or to just run it.
        /// 
        /// Here is an example of the suble difference:
        /// public async Task<int> ExecuteScalar(...)
        /// public Task<int> ExecuteScalar(...)
        /// </summary>
        public abstract bool ExecuteAsAsync { get; }

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var canWrap = WrapperNames.Contains(methodInfo.RequestedWrapperName, StringComparer.OrdinalIgnoreCase);

            if (canWrap && ExecuteAsAsync)
            {
                var method = methodInfo.Method;
                return TaskFriendlySyncContextValidator.CanWrapAsyncMethod(method.Type.Assembly.GetName().Name, method.Type.FullName, method.MethodName);
            }

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            //This only happens if we are in an async context.  Regardless if we are adding the after delegate as a contination.
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var sqlCommand = (IDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
            if (sqlCommand == null)
                return Delegates.NoOp;

            var sql = sqlCommand.CommandText ?? string.Empty;
            var vendor = SqlWrapperHelper.GetVendorName(sqlCommand);
            object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(vendor, sqlCommand.Connection.ConnectionString, agent.Configuration.UtilizationHostName);
            var connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(sqlCommand.Connection.ConnectionString, GetConnectionInfo);

            var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, sqlCommand.CommandType, sql);

            var queryParameters = SqlWrapperHelper.GetQueryParameters(sqlCommand, agent);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, connectionInfo, sql, queryParameters, isLeaf: true);

            switch (vendor)
            {
                case DatastoreVendor.MSSQL:
                    agent.EnableExplainPlans(segment, () => SqlServerExplainPlanActions.AllocateResources(sqlCommand), SqlServerExplainPlanActions.GenerateExplainPlan, null);
                    break;

                case DatastoreVendor.MySQL:
                    if (parsedStatement != null)
                    {
                        agent.EnableExplainPlans(segment, () => MySqlExplainPlanActions.AllocateResources(sqlCommand), MySqlExplainPlanActions.GenerateExplainPlan, () => MySqlExplainPlanActions.ShouldGenerateExplainPlan(sql, parsedStatement));
                    }
                    break;
            }

            return ExecuteAsAsync
                ? Delegates.GetAsyncDelegateFor<Task>(agent, segment)
                : Delegates.GetDelegateFor(segment);
        }

    }
}
