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

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers;

internal static class DynamoDbRequestHandler
{
    private static readonly ConcurrentDictionary<string, string> _operationNameCache = new();

    // per the spec, these are the only DynamoDB request types that we support
    // -- everything else should get a NoOp, which will allow the HttpClient instrumentation to create an external segment for the call.
    private static readonly HashSet<string> _supportedRequestTypes = new()
    {
        "CreateTableRequest",
        "DeleteItemRequest",
        "DeleteTableRequest",
        "GetItemRequest",
        "PutItemRequest",
        "QueryRequest",
        "ScanRequest",
        "UpdateItemRequest"
    };

    public static AfterWrappedMethodDelegate HandleDynamoDbRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, ArnBuilder builder)
    {
        var requestType = ((object)request).GetType().Name;
        if (!_supportedRequestTypes.Contains(requestType))
        {
            agent.Logger.Debug("DynamoDbRequestHandler: {requestType} is not a supported request type.", requestType);
            return Delegates.NoOp;
        }

        var operation = _operationNameCache.GetOrAdd(requestType, requestType.Replace("Request", string.Empty).ToSnakeCase());

        string model = null;
        try
        {
            model = request.TableName;
        }
        catch (Exception) // this shouldn't happen, as the TableName property should exist on all supported request types
        {
            agent.Logger.Debug("DynamoDbRequestHandler: {requestType} does not have a TableName property. Not building an ARN for this request.", requestType);
        }

        var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.DynamoDB, model, operation), isLeaf: true);

        // build the ARN if we have a model (table name)
        if (!string.IsNullOrEmpty(model))
        {
            var arn = builder.Build("dynamodb", $"table/{model}");
            if (!string.IsNullOrEmpty(arn))
                segment.AddCloudSdkAttribute("cloud.resource_id", arn);
        }

        segment.AddCloudSdkAttribute("aws.operation", operation);
        segment.AddCloudSdkAttribute("aws.region", builder.Region);

        return isAsync ?
            Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, responseTask =>
            {
                try
                {
                    if (responseTask.IsFaulted)
                        transaction.NoticeError(responseTask.Exception);
                    else
                        SetRequestIdIfAvailable(agent, segment, ((dynamic)responseTask).Result);
                }
                finally
                {
                    segment.End();
                }

            }, TaskContinuationOptions.ExecuteSynchronously)
            :
            Delegates.GetDelegateFor<object>(
                onFailure: segment.End,
                onSuccess: response =>
                {
                    SetRequestIdIfAvailable(agent, segment, response);
                    segment.End();
                }
            );

    }

    private static void SetRequestIdIfAvailable(IAgent agent, ISegment segment, dynamic response)
    {
        if (response != null && response.ResponseMetadata != null && response.ResponseMetadata.RequestId != null)
        {
            string requestId = response.ResponseMetadata.RequestId;
            segment.AddCloudSdkAttribute("aws.requestId", requestId);
        }
    }
}
