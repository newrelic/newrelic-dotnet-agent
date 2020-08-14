// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;


namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public class MongoDatabaseWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;
        private static readonly HashSet<string> CanExtractModelNameMethods = new HashSet<string>() { "CreateCollection", "CreateCollectionAsync", "DropCollection", "DropCollectionAsync" };

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;

            var canWrap = method.MatchesAny(assemblyName: "MongoDB.Driver", typeName: "MongoDB.Driver.MongoDatabaseImpl",
                methodNames: new[]
                {
                    "CreateCollection",
                    "CreateView",
                    "DropCollection",
                    "ListCollections",
                    "RenameCollection",
                    "RunCommand",

                    "CreateCollectionAsync",
                    "CreateViewAsync",
                    "DropCollectionAsync",
                    "ListCollectionsAsync",
                    "RenameCollectionAsync",
                    "RunCommandAsync"
                });

            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
            var model = TryGetModelName(instrumentedMethodCall);

            var caller = instrumentedMethodCall.MethodCall.InvocationTarget;
            ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(caller);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

            if (!operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
            {
                return Delegates.GetDelegateFor(segment);
            }

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true);
        }

        private string TryGetModelName(InstrumentedMethodCall instrumentedMethodCall)
        {
            var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;

            if (CanExtractModelNameMethods.Contains(methodName))
            {
                if (instrumentedMethodCall.MethodCall.MethodArguments[0] is string)
                {
                    return instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(0);
                }
                return instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<string>(1);
            }

            return null;
        }
    }
}
