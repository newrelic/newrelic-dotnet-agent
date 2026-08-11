// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Data;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Parsing.ConnectionString;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.Sql;

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

            // Read the connection string once and reuse it for vendor detection, the
            // cache key, and the parser call. This wrapper previously read it two or
            // three times per command execution, and every read allocates.
            var connection = odbcCommand.Connection;
            var connectionString = connection.ConnectionString;

            var vendor = SqlWrapperHelper.GetVendorNameFromOdbcConnectionString(connectionString);

            object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(vendor, connectionString, agent.Configuration.UtilizationHostName);
            var connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(connectionString, GetConnectionInfo);

            // The active database can be changed on an open connection (ChangeDatabase
            // or a USE statement) without the connection string changing, so trust the
            // live value over the parsed one when they disagree.
            connectionInfo = ConnectionInfoResolver.ResolveWithLiveDatabase(transaction, connectionInfo, connectionString, connection.Database);

            var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, odbcCommand.CommandType, sql);

            var queryParameters = SqlWrapperHelper.GetQueryParameters(odbcCommand, agent);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, connectionInfo, sql, queryParameters);

            return Delegates.GetDelegateFor(segment);
        }
    }
}