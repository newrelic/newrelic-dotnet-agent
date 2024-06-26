// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.MongoDb
{
    public class BulkWriteOperationExecuteWrapper : IWrapper
    {
        private const string WrapperName = "BulkWriteOperationExecuteWrapper";
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = GetOperationName(instrumentedMethodCall.MethodCall);
            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.MongoDB, operation));

            return Delegates.GetDelegateFor(segment);
        }

        private string GetOperationName(MethodCall methodCall)
        {
            if (methodCall.Method.MethodName == "Insert")
                return "BulkWriteOperation Insert";

            if (methodCall.Method.MethodName == "ExecuteHelper")
                return "BulkWriteOperation Execute";

            throw new Exception(string.Format("Method passed to BeforeWrappedMethod was unexpected. {0}", methodCall.Method.MethodName));
        }

    }
}
