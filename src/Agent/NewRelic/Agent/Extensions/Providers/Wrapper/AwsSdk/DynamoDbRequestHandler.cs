// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using static System.Collections.Specialized.BitVector32;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    internal static class DynamoDbRequestHandler
    {

        public static AfterWrappedMethodDelegate HandleDynamoDbRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, dynamic executionContext)
        {
            var requestType = request.GetType().Name;

            string model;
            string operation;
            switch (requestType)
            {
                case "CreateGlobalTableRequest":
                case "CreateTableRequest":
                    operation = "create_table";
                    break;
                case "DeleteTableRequest":
                    operation = "delete_table";
                    break;
                case "DeleteItemRequest":
                    operation = "delete_item";
                    break;
                case "TransactGetItemsRequest":
                case "BatchGetItemRequest":
                case "GetItemRequest":
                    operation = "get_item";
                    break;
                case "PutItemRequest":
                    operation = "put_item";
                    break;
                case "QueryRequest":
                    operation = "query";
                    break;
                case "ScanRequest":
                    operation = "scan";
                    break;
                case "UpdateItemRequest":
                    operation = "update_item";
                    break;
                /* All other request types
                case "DescribeEndpointsRequest":
                case "DescribeTableRequest":
                case "DescribeExportRequest":
                case "ExportTableToPointInTimeRequest":
                case "ListImportsRequest":
                case "BatchExecuteStatementRequest":
                case "DeleteResourcePolicyRequest":
                case "DescribeImportRequest":
                case "UpdateGlobalTableRequest":
                case "ListGlobalTablesRequest":
                case "DescribeTableReplicaAutoScalingRequest":
                case "ListBackupsRequest":
                case "ExecuteStatementRequest":
                case "DescribeContinuousBackupsRequest":
                case "UpdateContributorInsightsRequest":
                case "TagResourceRequest":
                case "ImportTableRequest":
                case "DescribeContributorInsightsRequest":
                case "EnableKinesisStreamingDestinationRequest":
                case "DescribeGlobalTableSettingsRequest":
                case "UpdateTableReplicaAutoScalingRequest":
                case "UntagResourceRequest":
                case "GetResourcePolicyRequest":
                case "DeleteBackupRequest":
                case "UpdateTimeToLiveRequest":
                case "RestoreTableToPointInTimeRequest":
                case "DescribeTimeToLiveRequest":
                case "UpdateContinuousBackupsRequest":
                case "DisableKinesisStreamingDestinationRequest":
                case "ListContributorInsightsRequest":
                case "ListTagsOfResourceRequest":
                case "DescribeBackupRequest":
                case "DescribeKinesisStreamingDestinationRequest":
                case "UpdateKinesisStreamingDestinationRequest":
                case "BatchWriteItemRequest":
                case "ListExportsRequest":
                case "RestoreTableFromBackupRequest":
                case "DescribeLimitsRequest":
                case "ListTablesRequest":
                case "PutResourcePolicyRequest":
                case "UpdateGlobalTableSettingsRequest":
                case "CreateBackupRequest":
                case "TransactWriteItemsRequest":
                case "ExecuteTransactionRequest":
                */
                default:
                    operation = "other";
                    break;
            }
            // Even though there is no common interface they all implement, every Request type I checked
            // has a TableName property
            model = request.TableName;

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.DynamoDB, model, operation));
            return isAsync ?
                Delegates.GetAsyncDelegateFor<Task>(agent, segment)
                :
                Delegates.GetDelegateFor(segment);
        }
    }
}
