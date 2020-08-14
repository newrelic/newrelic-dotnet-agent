// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public class MongoCollectionBaseWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollectionBase`1",
                methodNames: new[]
                {
                    "DeleteMany",
                    "DeleteOne",
                    "InsertOne",
                    "InsertMany",
                    "ReplaceOne",
                    "UpdateMany",
                    "UpdateOne",

                    "DeleteManyAsync",
                    "DeleteOneAsync",
                    "InsertOneAsync",
                    "InsertManyAsync",
                    "ReplaceOneAsync",
                    "UpdateManyAsync",
                    "UpdateOneAsync"
                });

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
            var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

            var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
            var model = MongoDbHelper.GetCollectionName(collectionNamespace);

            var database = MongoDbHelper.GetDatabaseFromGeneric(caller);

            ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database);

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
