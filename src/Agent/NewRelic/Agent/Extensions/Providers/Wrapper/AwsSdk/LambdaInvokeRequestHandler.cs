// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.AwsSdk;

namespace NewRelic.Providers.Wrapper.AwsSdk
{
    internal static class LambdaInvokeRequestHandler
    {
        private static Func<object, object> _getResultFromGenericTask;
        private static ConcurrentDictionary<string, string> _arnCache = new ConcurrentDictionary<string, string>();
        private static bool _reportMissingRequestId = true;

        private static object GetTaskResult(object task)
        {
            if (((Task)task).IsFaulted)
            {
                return null;
            }

            var getResponse = _getResultFromGenericTask ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(task.GetType(), "Result");
            return getResponse(task);
        }

        private static void SetRequestIdIfAvailable(IAgent agent, ITransaction transaction, dynamic response)
        {
            try
            {
                dynamic metadata = response.ResponseMetadata;
                string requestId = metadata.RequestId;
                transaction.AddCloudSdkAttribute("aws.requestId", requestId);
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
                string key = $"{builder.Region}:{functionName}";
                if (!_arnCache.TryGetValue(key, out arn))
                {
                    arn = builder.Build("function", functionName);
                    _arnCache.TryAdd(key, arn);
                }
            }
            var segment = transaction.StartTransactionSegment(instrumentedMethodCall.MethodCall, "InvokeRequest");
            //segment.GetExperimentalApi().MakeLeaf();

            transaction.AddCloudSdkAttribute("cloud.platform", "aws_lambda");
            transaction.AddCloudSdkAttribute("aws.operation", "InvokeRequest");
            transaction.AddCloudSdkAttribute("aws.region", builder.Region);


            if (!string.IsNullOrEmpty(arn))
            {
                transaction.AddCloudSdkAttribute("cloud.resource_id", arn);
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
                        SetRequestIdIfAvailable(agent, transaction, GetTaskResult(responseTask));
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
                        SetRequestIdIfAvailable(agent, transaction, response);
                        segment.End();
                    }
                );
            }
        }
    }
}
