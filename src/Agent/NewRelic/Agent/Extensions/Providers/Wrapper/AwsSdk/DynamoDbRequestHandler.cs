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
            var requestType = ((object)request).GetType().Name;

            // PutItemRequest => put_item,
            // CreateTableRequest => create_table, etc.
            var operation = _operationNameCache.GetOrAdd(requestType, GetOperationNameFromRequestType);

            // Even though there is no common interface they all implement, every Request type I checked
            // has a TableName property
            string model = request.TableName;

            string region = executionContext.RequestContext.ClientConfig.RegionEndpoint.SystemName; // TODO: This might need some null checking
            string arn = GetArnFromTableName(model, region);
            transaction.AddFaasAttribute("cloud.resource_id", arn); // TODO: Not 100% positive this is the correct attribute name; need to verify

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

        private static string GetArnFromTableName(string tableName, string region)
        {
            var accountId = AmazonServiceClientWrapper.AwsAccountId ?? "(unknown)";
            return $"arn:aws:dynamodb:{region}:{accountId}:table/{tableName}";
        }
    }
}
