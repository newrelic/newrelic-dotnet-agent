// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.SQS.Model;
using NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver;
using NewRelic.OpenTracing.AmazonLambda.Helpers;
using OpenTracing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.OpenTracing.AmazonLambda
{
    public class SQSWrapper
    {
        private const string Component = "SQS";

        #region Public wrapper methods

        /// <summary>
        /// Wrap an SQS SendMessageAsync request given a SendMessageRequest object
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageRequest object and returns a Task of type SendMessageResponse, e.g. AmazonSQSClient.SendMessageAsync</param>
        /// <param name="sendMessageRequest">An Amazon.SQS.Model.SendMessageRequest object</param>
        public static Task<SendMessageResponse> WrapRequest(Func<SendMessageRequest, Task<SendMessageResponse>> handler, SendMessageRequest sendMessageRequest)
        {
            return WrapSendMessageRequest(handler, sendMessageRequest);
        }

        /// <summary>
        /// Wrap an SQS SendMessageAsync request given a SendMessageRequest object
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageRequest object and a CancellationToken, and returns a Task of type SendMessageResponse, e.g. AmazonSQSClient.SendMessageAsync</param>
        /// <param name="sendMessageRequest">An Amazon.SQS.Model.SendMessageRequest object</param>
        /// <param name="cancellationToken">An optional CancellationToken object</param>
        public static Task<SendMessageResponse> WrapRequest(Func<SendMessageRequest, CancellationToken, Task<SendMessageResponse>> handler, SendMessageRequest sendMessageRequest, CancellationToken cancellationToken = default)
        {
            return WrapSendMessageRequest(handler, sendMessageRequest, cancellationToken);
        }

        /// <summary>
        /// Wrap an SQS SendMessageAsync request given an SQS queue URL and a message body, from which a new SendMessageRequest object will be created
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageRequest object and a CancellationToken, and returns a Task of type SendMessageResponse, e.g. AmazonSQSClient.SendMessageAsync</param>
        /// <param name="queueUrl">A string that contains an SQS queue URL</param>
        /// <param name="messageBody">A string that contains the body of the message to send</param>
        /// <param name="cancellationToken">An optional CancellationToken object</param>
        public static Task<SendMessageResponse> WrapRequest(Func<SendMessageRequest, CancellationToken, Task<SendMessageResponse>> handler, string queueUrl, string messageBody, CancellationToken cancellationToken = default)
        {
            var sendMessageRequest = new SendMessageRequest(queueUrl, messageBody);
            return WrapSendMessageRequest(handler, sendMessageRequest, cancellationToken);
        }

        /// <summary>
        /// Wrap an SQS SendMessageBatchAsync request given a SendMessageBatchRequest object
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageBatchResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageBatchRequest object and returns a Task of type SendMessageBatchResponse, e.g. AmazonSQSClient.SendMessageBatchAsync</param>
        /// <param name="sendMessageBatchRequest">An Amazon.SQS.Model.SendMessageBatchRequest object</param>
        public static Task<SendMessageBatchResponse> WrapRequest(Func<SendMessageBatchRequest, Task<SendMessageBatchResponse>> handler, SendMessageBatchRequest sendMessageBatchRequest)
        {
            return WrapSendMessageBatchRequest(handler, sendMessageBatchRequest);
        }

        /// <summary>
        /// Wrap an SQS SendMessageBatchAsync request given a SendMessageBatchRequest object
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageBatchResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageBatchRequest object and returns a Task of type SendMessageBatchResponse, e.g. AmazonSQSClient.SendMessageBatchAsync</param>
        /// <param name="sendMessageBatchRequest">An Amazon.SQS.Model.SendMessageBatchRequest object</param>
        /// <param name="cancellationToken">An optional CancellationToken object</param>
        public static Task<SendMessageBatchResponse> WrapRequest(Func<SendMessageBatchRequest, CancellationToken, Task<SendMessageBatchResponse>> handler, SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken = default)
        {
            return WrapSendMessageBatchRequest(handler, sendMessageBatchRequest, cancellationToken);
        }

        /// <summary>
        /// Wrap an SQS SendMessageBatchAsync request given an SQS queue URL and a list of SendMessageBatchRequestEntry objects, from which a new SendMessageBatchRequest object will be created
        /// This will create a client span with component "SQS" and adds the appropriate distributed tracing attribute to the message
        /// Note that this will only have an effect if the environment variable "NEW_RELIC_USE_DT_WRAPPER" is set to "true"
        /// </summary>
        /// <returns>
        /// A Task with type SendMessageBatchResponse
        /// </returns>
        /// <param name="handler">A function which takes a SendMessageBatchRequest object and returns a Task of type SendMessageBatchResponse, e.g. AmazonSQSClient.SendMessageBatchAsync</param>
        /// <param name="queueUrl">A string containing an SQS queue URL</param>
        /// <param name="entries">A list of Amazon.SQS.Model.SendMessageBatchRequestEntry objects</param>
        /// <param name="cancellationToken">An optional CancellationToken object</param>
        public static Task<SendMessageBatchResponse> WrapRequest(Func<SendMessageBatchRequest, CancellationToken, Task<SendMessageBatchResponse>> handler, string queueUrl, List<SendMessageBatchRequestEntry> entries, CancellationToken cancellationToken = default)
        {
            var sendMessageBatchRequest = new SendMessageBatchRequest(queueUrl, entries);
            return WrapSendMessageBatchRequest(handler, sendMessageBatchRequest, cancellationToken);
        }

        #endregion

        #region Private wrapper helpers

        private static Task<SendMessageResponse> WrapSendMessageRequest(Func<SendMessageRequest, CancellationToken, Task<SendMessageResponse>> handler, SendMessageRequest sendMessageRequest, CancellationToken cancellationToken = default)
        {
            var span = BeforeWrappedMethod(sendMessageRequest);
            var result = handler(sendMessageRequest, cancellationToken);
            result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
            return result;
        }

        private static Task<SendMessageResponse> WrapSendMessageRequest(Func<SendMessageRequest, Task<SendMessageResponse>> handler, SendMessageRequest sendMessageRequest)
        {
            var span = BeforeWrappedMethod(sendMessageRequest);
            var result = handler(sendMessageRequest);
            result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
            return result;
        }

        private static Task<SendMessageBatchResponse> WrapSendMessageBatchRequest(Func<SendMessageBatchRequest, CancellationToken, Task<SendMessageBatchResponse>> handler, SendMessageBatchRequest sendMessageBatchRequest, CancellationToken cancellationToken = default)
        {
            var span = BeforeWrappedMethod(sendMessageBatchRequest);
            var result = handler(sendMessageBatchRequest, cancellationToken);
            result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
            return result;
        }

        private static Task<SendMessageBatchResponse> WrapSendMessageBatchRequest(Func<SendMessageBatchRequest, Task<SendMessageBatchResponse>> handler, SendMessageBatchRequest sendMessageBatchRequest)
        {
            var span = BeforeWrappedMethod(sendMessageBatchRequest);
            var result = handler(sendMessageBatchRequest);
            result.ContinueWith((task) => ServiceHelpers.AfterWrappedMethod(span, task), TaskContinuationOptions.ExecuteSynchronously);
            return result;
        }

        #endregion

        #region BeforeWrappedMethod

        private static ISpan BeforeWrappedMethod(object request)
        {
            if (AwsServiceHandler.UseDTWrapper)
            {
                ISpan span = null;
                switch (request.GetType().ToString())
                {
                    case "Amazon.SQS.Model.SendMessageRequest":
                        var sendMessageRequest = (SendMessageRequest)request;
                        span = ServiceHelpers.CreateSpan(sendMessageRequest.GetOperationName(ServiceHelpers.ProduceOperation), Component, ServiceHelpers.ProduceOperation);
                        span.ApplyDistributedTracePayload(sendMessageRequest.MessageAttributes);
                        break;
                    case "Amazon.SQS.Model.SendMessageBatchRequest":
                        var sendMessageBatchRequest = (SendMessageBatchRequest)request;
                        span = ServiceHelpers.CreateSpan(sendMessageBatchRequest.GetOperationName(ServiceHelpers.ProduceOperation), Component, ServiceHelpers.ProduceOperation);
                        foreach (var entry in sendMessageBatchRequest.Entries)
                        {
                            span.ApplyDistributedTracePayload(entry.MessageAttributes);
                        }
                        break;
                    default:
                        break;
                }

                return span;
            }
            else
            {
                return null;
            }
        }

        #endregion
    }
}
