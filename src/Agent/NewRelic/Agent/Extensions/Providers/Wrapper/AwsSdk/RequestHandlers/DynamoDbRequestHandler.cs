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

        public static AfterWrappedMethodDelegate HandleDynamoDbRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, ArnBuilder builder)
        {
            var requestType = ((object)request).GetType().Name;

            var operation = _operationNameCache.GetOrAdd(requestType, requestType.Replace("Request", string.Empty).ToSnakeCase());

            // all request objects implement a TableName property
            string model = request.TableName;

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, new ParsedSqlStatement(DatastoreVendor.DynamoDB, model, operation), isLeaf: true);

            var arn = builder.Build("dynamodb", $"table/{model}");
            if (!string.IsNullOrEmpty(arn))
                segment.AddCloudSdkAttribute("cloud.resource_id", arn);
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
}
