// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Data.OleDb;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.Sql
{
	public class OleDbCommandWrapper : IWrapper
	{
		public const string WrapperName = "OleDbCommandTracer";

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			return new CanWrapResponse(methodInfo.RequestedWrapperName.Equals(WrapperName, StringComparison.OrdinalIgnoreCase));
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			{
				var oleDbCommand = (OleDbCommand)instrumentedMethodCall.MethodCall.InvocationTarget;
				if (oleDbCommand == null)
					return Delegates.NoOp;

				var sql = oleDbCommand.CommandText ?? string.Empty;
				var vendor = SqlWrapperHelper.GetVendorName(oleDbCommand);

				var parsedStatement = transaction.GetParsedDatabaseStatement(vendor, oleDbCommand.CommandType, sql);

				var queryParameters = SqlWrapperHelper.GetQueryParameters(oleDbCommand, agent);

				var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, parsedStatement, null, sql, queryParameters);

				return Delegates.GetDelegateFor(segment);
			}
		}
	}
}
#endif
