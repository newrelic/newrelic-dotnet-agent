// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MongoDB.Driver;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.MongoDb
{
    public class MongoCollectionRemoveWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollection", methodName: "Remove");
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = GetRemoveOperationName(instrumentedMethodCall.MethodCall);
            var model = MongoDBHelper.GetCollectionModelName(instrumentedMethodCall.MethodCall);
            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation));

            return Delegates.GetDelegateFor(segment);
        }

        private string GetRemoveOperationName(MethodCall methodCall)
        {
            try
            {
                var removeFlags = ((RemoveFlags)methodCall.MethodArguments[1]);
                if (methodCall.MethodArguments[0] == null &&
                    removeFlags == RemoveFlags.None)
                    return "RemoveAll";
            }
            catch (Exception e)
            {
                throw new Exception("Expected a MongoDB.Driver.RemoveFlags as the second method argument.", e);
            }

            return "Remove";
        }
    }
}
