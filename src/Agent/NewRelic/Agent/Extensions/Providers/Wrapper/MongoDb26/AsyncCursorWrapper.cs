/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public class AsyncCursorWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver.Core", typeName: "MongoDB.Driver.Core.Operations.AsyncCursor`1",
                methodSignatures: new[]
                {
                    new MethodSignature("GetNextBatch", "System.Threading.CancellationToken"),
                    new MethodSignature("GetNextBatchAsync", "System.Threading.CancellationToken"),
                });

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;

            var caller = instrumentedMethodCall.MethodCall.InvocationTarget;
            var collectionNamespace = MongoDbHelper.GetCollectionNamespaceFieldFromGeneric(caller);
            var model = MongoDbHelper.GetCollectionName(collectionNamespace);

            var connectionInfo = MongoDbHelper.GetConnectionInfoFromCursor(caller, collectionNamespace);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true,
                connectionInfo: connectionInfo);

            if (!operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                return Delegates.GetDelegateFor(segment);
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true);
        }
    }
}
