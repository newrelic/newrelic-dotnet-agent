// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public class MongoIndexManagerBaseWrapper : IWrapper
    {
        private const string WrapperName = "MongoIndexManagerBaseWrapper";
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
            var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

            var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
            var model = MongoDbHelper.GetCollectionName(collectionNamespace);

            var collection = MongoDbHelper.GetCollectionFieldFromGeneric(caller);
            var database = MongoDbHelper.GetDatabaseFromGeneric(collection);

            ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database, agent.Configuration.UtilizationHostName);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

            if (!operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                return Delegates.GetDelegateFor(segment);
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true);
        }
    }
}
