// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET35
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

                var sql = oleDbCommand.CommandText ?? string.Empty;
                var vendor = SqlWrapperHelper.GetVendorName(oleDbCommand);

                var parsedStatement = transaction.GetParsedDatabaseStatement(oleDbCommand.CommandType, sql);
                var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement?.Operation, vendor, parsedStatement?.Model, sql);

                return Delegates.GetDelegateFor(segment);
            }
        }
    }
}
#endif
