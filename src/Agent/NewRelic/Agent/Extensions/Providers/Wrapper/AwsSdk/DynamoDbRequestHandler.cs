// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    internal static class DynamoDbRequestHandler
    {

        private static ConcurrentDictionary<string,string> _operationNameCache = new ConcurrentDictionary<string,string>();

        public static AfterWrappedMethodDelegate HandleDynamoDbRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, dynamic executionContext)
        {
            var requestType = request.GetType().Name as string;

            string model;
            string operation;

            // PutItemRequest => put_item,
            // CreateTableRequest => create_table, etc.
            operation = _operationNameCache.GetOrAdd(requestType, GetOperationNameFromRequestType(requestType));

            // Even though there is no common interface they all implement, every Request type I checked
            // has a TableName property
            model = request.TableName;

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.DynamoDB, model, operation));
            return isAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment)
                :
                Delegates.GetDelegateFor(segment);
        }

        private static string GetOperationNameFromRequestType(string requestType)
        {
            return requestType.Replace("Request", string.Empty).ToSnakeCase();
        }
    }
}
