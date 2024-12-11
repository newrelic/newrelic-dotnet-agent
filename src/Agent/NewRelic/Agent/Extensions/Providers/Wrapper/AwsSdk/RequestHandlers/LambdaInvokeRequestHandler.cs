// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.AwsSdk;

namespace NewRelic.Providers.Wrapper.AwsSdk.RequestHandlers
{
    internal static class LambdaInvokeRequestHandler
    {
        private static Func<object, object> _getResultFromGenericTask;
        private static readonly ConcurrentDictionary<string, string> _arnCache = new();
        private static bool _reportMissingRequestId = true;
        private static bool _reportBadInvocationName = true;
        private const int MAX_CACHE_SIZE = 25;  // Shouldn't ever get this big, but just in case

        private static object GetTaskResult(object task)
        {
            if (((Task)task).IsFaulted)
            {
                return null;
            }

            var getResponse = _getResultFromGenericTask ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(task.GetType(), "Result");
            return getResponse(task);
        }

        private static void SetRequestIdIfAvailable(IAgent agent, ISegment segment, dynamic response)
        {
            try
            {
                dynamic metadata = response.ResponseMetadata;
                string requestId = metadata.RequestId;
                segment.AddCloudSdkAttribute("aws.requestId", requestId);
            }
            catch (Exception e)
            {
                if (_reportMissingRequestId)
                {
                    agent.Logger.Debug(e, "Unable to get RequestId from response metadata.");
                    _reportMissingRequestId = false;
                }
            }
        }

        public static AfterWrappedMethodDelegate HandleInvokeRequest(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction, dynamic request, bool isAsync, ArnBuilder builder)
        {
            string functionName = request.FunctionName;
            string qualifier = request.Qualifier;
            if (!string.IsNullOrEmpty(qualifier) && !functionName.EndsWith(qualifier))
            {
                functionName = $"{functionName}:{qualifier}";
            }
            string arn;
            if (functionName.StartsWith("arn:"))
            {
                arn = functionName;
            }
            else
            {
                if (!_arnCache.TryGetValue(functionName, out arn))
                {
                    arn = builder.BuildFromPartialLambdaArn(functionName);
                    if (_arnCache.Count < MAX_CACHE_SIZE)
                    {
                        _arnCache.TryAdd(functionName, arn);
                    }
                }
            }
            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "InvokeRequest");

            segment.AddCloudSdkAttribute("cloud.platform", "aws_lambda");
            segment.AddCloudSdkAttribute("aws.operation", "InvokeRequest");
            segment.AddCloudSdkAttribute("aws.region", builder.Region);


            if (!string.IsNullOrEmpty(arn))
            {
                segment.AddCloudSdkAttribute("cloud.resource_id", arn);
            }
            else if (_reportBadInvocationName)
            {
                agent.Logger.Debug($"Unable to resolve Lambda invocation named '{functionName}' [{builder.ToString()}]");
                _reportBadInvocationName = false;
            }

            if (isAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    try
                    {
                        if (responseTask.Status == TaskStatus.Faulted)
                        {
                            transaction.NoticeError(responseTask.Exception);
                        }
                        SetRequestIdIfAvailable(agent, segment, GetTaskResult(responseTask));
                    }
                    finally
                    {
                        segment?.End();
                    }
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                    onFailure: ex => segment.End(ex),
                    onSuccess: response =>
                    {
                        SetRequestIdIfAvailable(agent, segment, response);
                        segment.End();
                    }
                );
            }
        }
    }
}
