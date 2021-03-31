// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public class MongoQueryProviderImplWrapper : IWrapper
    {
        private const string WrapperName = "MongoQueryProviderImplWrapper";
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
            var isAsync = operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase);
            operation = isAsync ? "LinqQueryAsync" : "LinqQuery";

            var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

            var collection = MongoDbHelper.GetCollectionFieldFromGeneric(caller);
            var database = MongoDbHelper.GetDatabaseFromGeneric(collection);

            ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database);
            var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
            var model = MongoDbHelper.GetCollectionName(collectionNamespace);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

            if (!isAsync)
            {
                return Delegates.GetDelegateFor(segment);
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true);
        }
    }
}
