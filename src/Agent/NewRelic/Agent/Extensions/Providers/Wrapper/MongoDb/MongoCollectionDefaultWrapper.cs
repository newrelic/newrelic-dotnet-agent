// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.MongoDb
{
    public class MongoCollectionDefaultWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoCollection",
                methodSignatures: new[]
                {
                    new MethodSignature("Aggregate"),
                    new MethodSignature("CreateIndex", "MongoDB.Driver.IMongoIndexKeys,MongoDB.Driver.IMongoIndexOptions"),
                    new MethodSignature("Drop"),
                    new MethodSignature("FindAndModify"),
                    new MethodSignature("FindAndRemove"),
                    new MethodSignature("GetIndexes"),
                    new MethodSignature("IndexExistsByName"),
                    new MethodSignature("InitializeOrderedBulkOperation"),
                    new MethodSignature("InitializeUnorderedBulkOperation"),
                    new MethodSignature("ParallelScanAs", "MongoDB.Driver.ParallelScanArgs"),
                    new MethodSignature("Save", "System.Type,System.Object,MongoDB.Driver.MongoInsertOptions"),
                    new MethodSignature("Update", "MongoDB.Driver.IMongoQuery,MongoDB.Driver.IMongoUpdate,MongoDB.Driver.MongoUpdateOptions"),
                    new MethodSignature("Validate", "MongoDB.Driver.ValidateCollectionArgs")
                });
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
            var model = MongoDBHelper.GetCollectionModelName(instrumentedMethodCall.MethodCall);
            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation));

            return Delegates.GetDelegateFor(segment);
        }
    }
}
