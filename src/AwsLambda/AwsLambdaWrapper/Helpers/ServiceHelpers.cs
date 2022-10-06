// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver;
using OpenTracing;
using OpenTracing.Tag;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NewRelic.OpenTracing.AmazonLambda.Helpers
{
    internal static class ServiceHelpers
    {
        internal const string ProduceOperation = "Produce";

        /// <summary>
        /// Create a Span based on name/component/operation
        /// </summary>
        /// <param name="operationPair">operation name, operation</param>
        /// <param name="component"></param>
        /// <returns></returns>
        internal static ISpan CreateSpan(KeyValuePair<string, string> operationPair, string component)
        {
            return CreateSpan(operationPair.Key, component, operationPair.Value);
        }

        /// <summary>
        /// Create a Span based on name/component/operation
        /// </summary>
        /// <param name="operationName"></param>
        /// <param name="component"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        internal static ISpan CreateSpan(string operationName, string component, string operation)
        {

            operationName = string.IsNullOrEmpty(operationName) ? "UNKNOWN" : operationName;
            component = string.IsNullOrEmpty(component) ? "UNKNOWN" : component;
            operation = string.IsNullOrEmpty(operation) ? "UNKNOWN" : operation;

            var span = GlobalTracer.Instance.BuildSpan(operationName).Start();
            span.SetTag(Tags.SpanKind, Tags.SpanKindClient);
            span.SetTag(Tags.Component, component);
            span.SetTag("aws.operation", operation);
            return span;
        }

        internal static void AfterWrappedMethod<T>(ISpan span, Task<T> task)
        {
            if (AwsServiceHandler.UseDTWrapper)
            {
                if (span == null)
                {
                    return;
                }

                var statusCode = GetHttpStatusCode(task);
                if (task.IsFaulted)
                {
                    span.SetTag(Tags.Error, true);
                    var ex = task.Exception;
                    if (ex.InnerException != null)
                    {
                        if (ex.InnerException is AggregateException && ex.InnerException.InnerException != null)
                        {
                            span.SetException(ex.InnerException.InnerException);
                        }
                        else
                        {
                            span.SetException(ex.InnerException);
                        }
                    }
                    else
                    {
                        span.SetException(ex);
                    }
                }
                else if (task.IsCanceled)
                {
                    span.SetTag(Tags.Error, true);
                }
                else if (statusCode != null && statusCode.HasValue)
                {
                    span.SetTag(Tags.HttpStatus, statusCode.Value);
                }

                span.Finish();
            }
        }

        private static int? GetHttpStatusCode<T>(Task<T> response)
        {
            if (response.IsCanceled || response.IsFaulted)
            {
                return null;
            }

            switch (response.Result.GetType().ToString())
            {
                case "Amazon.SQS.Model.SendMessageResponse":
                    return GetHttpStatusCodeForSendMessageResponse(response.Result);
                case "Amazon.SQS.Model.SendMessageBatchResponse":
                    return GetHttpStatusCodeForSendMessageBatchResponse(response.Result);
                case "Amazon.SimpleNotificationService.Model.PublishResponse":
                    return GetHttpStatusCodeForPublishResponse(response.Result);
                default:
                    return null;
            }
        }

        private static int? GetHttpStatusCodeForSendMessageResponse(object response)
        {
            if (response is SendMessageResponse sendMessageResponse)
            {
                return (int)sendMessageResponse.HttpStatusCode;
            }

            return null;
        }

        private static int? GetHttpStatusCodeForSendMessageBatchResponse(object response)
        {
            if (response is SendMessageBatchResponse sendMessageBatchResponse)
            {
                return (int)sendMessageBatchResponse.HttpStatusCode;
            }

            return null;
        }

        private static int? GetHttpStatusCodeForPublishResponse(object response)
        {
            if (response is PublishResponse publishRespons)
            {
                return (int)publishRespons.HttpStatusCode;
            }

            return null;
        }
    }
}
