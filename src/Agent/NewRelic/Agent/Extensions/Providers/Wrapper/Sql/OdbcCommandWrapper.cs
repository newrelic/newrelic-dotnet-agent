// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Parsing.ConnectionString;
using System.Data;

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
                if (instrumentedMethodCall.MethodCall.InvocationTarget is not IDbCommand odbcCommand)
                {
                    return Delegates.NoOp;
                }

                var sql = odbcCommand.CommandText;

                var vendor = SqlWrapperHelper.GetVendorNameFromOdbcConnectionString(odbcCommand.Connection.ConnectionString);

                object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(vendor, odbcCommand.Connection.ConnectionString, agent.Configuration.UtilizationHostName);
                var connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(odbcCommand.Connection.ConnectionString, GetConnectionInfo);

                var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, odbcCommand.CommandType, sql);

                var queryParameters = SqlWrapperHelper.GetQueryParameters(odbcCommand, agent);

                var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, connectionInfo, sql, queryParameters);

                return Delegates.GetDelegateFor(segment);
            }
        }
    }
}
