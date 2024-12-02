// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers
{
    internal static class DynamoDbRequestHandler
    {

        private static readonly ConcurrentDictionary<string, string> _operationNameCache = new();

        public static AfterWrappedMethodDelegate HandleDynamoDbRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, dynamic executionContext, ArnBuilder builder)
        {
            var requestType = ((object)request).GetType().Name;

            // PutItemRequest => put_item,
            // CreateTableRequest => create_table, etc.
            var operation = _operationNameCache.GetOrAdd(requestType, GetOperationNameFromRequestType);

            // Even though there is no common interface they all implement, every Request type I checked
            // has a TableName property
            string model = request.TableName;

            // TODO: The entity relationship docs suggest cloud.resource_id should be a span attribute, so maybe we added it to the DataStore segment below instead??
            var arn = builder.Build("dynamodb", $"table/{model}");
            if (string.IsNullOrEmpty(arn))
                transaction.AddCloudSdkAttribute("cloud.resource_id", arn);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.DynamoDB, model, operation), isLeaf: true);

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
