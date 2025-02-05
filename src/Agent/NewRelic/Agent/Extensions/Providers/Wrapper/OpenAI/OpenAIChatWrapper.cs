// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Net;
using System.IO;
using System.Linq;
using NewRelic.Agent.Extensions.JsonConverters;
using NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads;

namespace NewRelic.Providers.Wrapper.OpenAI
{
    public class OpenAIChatWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
        private static ConcurrentDictionary<string, string> _libraryVersions = new();
        private const string WrapperName = "OpenAIChat";
        private const string VendorName = "openai";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall,
            IAgent agent, ITransaction transaction)
        {
            // Don't do anything, including sending the version Supportability metric, if we're disabled
            if (!agent.Configuration.AiMonitoringEnabled)
            {
                return Delegates.NoOp;
            }

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
            }

            var modelFieldAccessor =
                VisibilityBypasser.Instance.GenerateFieldReadAccessor<string>(
                    instrumentedMethodCall.MethodCall.InvocationTarget.GetType(), "_model");
            string model = modelFieldAccessor(instrumentedMethodCall.MethodCall.InvocationTarget);


            dynamic chatMessages = instrumentedMethodCall.MethodCall.MethodArguments[0];

            // we only support text completions for now
            // TODO: possible values are Text, Image and Refusal. What does Refusal mean?
            if (chatMessages[0].Content[0].Kind.ToString() != "Text")
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Debug,
                    $"Error processing request: Only text completions are supported, but got {chatMessages[0].Content[0].Type}");
                return Delegates.NoOp;
            }

            dynamic chatCompletionOptions = instrumentedMethodCall.MethodCall.MethodArguments[1]; // may be null

            var operationType = "completion";
            var methodMethodName =
                $"Llm/{operationType}/{VendorName}/{instrumentedMethodCall.MethodCall.Method.MethodName}";
            var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, methodMethodName);

            // required per spec
            var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule
                .Assembly.FullName);
            agent.RecordSupportabilityMetric($"DotNet/ML/{VendorName}/{version}");

            //if (instrumentedMethodCall.IsAsync)
            //{
            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                TryProcessAsyncResponse,
                TaskContinuationOptions.ExecuteSynchronously
            );
            //}
            //else
            //{
            //    return Delegates.GetDelegateFor<Task>(
            //        onComplete: Pro
            //    );
            //}

            void TryProcessAsyncResponse(Task responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    HandleError(segment, model, chatMessages, responseTask, agent);
                    segment.End();
                    return;
                }

                // We need the duration, so we end the segment before creating the events.
                segment.End();

                dynamic clientResult = GetTaskResult(responseTask);
                if (clientResult == null)
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn,
                        $"Error processing response: Response payload is null");
                    return;
                }

                ProcessResponse(segment, model, chatMessages, clientResult, chatCompletionOptions, agent);
            }
        }

        private string GetOrAddLibraryVersion(string assemblyFullName)
        {
            return _libraryVersions.GetOrAdd(assemblyFullName, VersionHelpers.GetLibraryVersion(assemblyFullName));
        }

        private static object GetTaskResult(object task)
        {
            if (((Task)task).IsFaulted)
            {
                return null;
            }

            var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(),
                t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
            return getResponse(task);
        }


        private void ProcessResponse(ISegment segment, string model, dynamic chatMessages, dynamic clientResult,
            dynamic chatCompletionOptions, IAgent agent)
        {
            Func<object, object> rolePropertyAccessor =
                VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(chatMessages[0].GetType(), "Role");

            string promptRole = rolePropertyAccessor(chatMessages[0]).ToString();

            string requestPrompt = "";
            foreach (dynamic chatMessage in chatMessages)
            {
                foreach (dynamic contentPart in chatMessage.Content)
                {
                    if (!string.IsNullOrEmpty(requestPrompt))
                    {
                        requestPrompt += " ";
                    }

                    requestPrompt += contentPart.Text;
                }
            }

            dynamic chatCompletionResponse = clientResult.Value;
            string responseModel = chatCompletionResponse.Model;

            Func<object, object> responseFieldGetter =
                VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>("System.ClientModel",
                    "System.ClientModel.ClientResult", "_response");
            dynamic response = responseFieldGetter(clientResult);
            var headers = response.Headers;

            var headersDictionary = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                var headerKey = header.Key;
                var headerValue = header.Value as string;
                headersDictionary.Add(headerKey, headerValue);
            }

            var llmHeaders = headersDictionary.GetOpenAiHeaders();
            string organization = headersDictionary.TryGetOpenAiOrganization();

            string finishReason = chatCompletionResponse.FinishReason.ToString();
            string responseRole = chatCompletionResponse.Role.ToString();
            string chatCompletionId = chatCompletionResponse.Id;

            int numMessages = 2; // OpenAI says they use one prompt and one response
            string responseContent = chatCompletionResponse.Content[0].Text;

            string completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                chatCompletionId,
                chatCompletionOptions != null ? chatCompletionOptions.Temperature : 0,
                chatCompletionOptions != null ? chatCompletionOptions.MaxOutputTokenCount : 0,
                model,
                responseModel,
                numMessages,
                finishReason,
                VendorName,
                false,
                llmHeaders,
                null,
                organization
            );

            // Prompt
            EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                chatCompletionId,
                responseModel,
                requestPrompt,
                promptRole,
                0,
                completionId,
                false,
                VendorName);

            // Response
            EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                chatCompletionResponse.Id,
                responseModel,
                responseContent,
                responseRole,
                1,
                completionId,
                true,
                VendorName);
        }

        private void HandleError(ISegment segment, string model, dynamic chatMessages, Task responseTask, IAgent agent)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Info,
                $"Error invoking OpenAI model {chatMessages}: {responseTask.Exception!.Message}");

            dynamic oaiException = responseTask.Exception!.InnerException;
            if (oaiException == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn,
                    $"Error invoking model {model}: Task faulted but there was no inner exception");
                return;
            }

            HttpStatusCode statusCode = oaiException.StatusCode;
            string errorCode = oaiException.ErrorCode;
            string errorMessage = oaiException.Message;
            string requestId = oaiException.RequestId;

            var errorData = new LlmErrorData
            {
                HttpStatusCode = ((int)statusCode).ToString(),
                ErrorCode = errorCode,
                ErrorParam = null, // not available in AWS
                ErrorMessage = errorMessage
            };

            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                requestId,
                0, //requestPayload.Temperature,
                0, //requestPayload.MaxTokens,
                model,
                null,
                0,
                null,
                VendorName,
                true,
                null,
                errorData);

            // Prompt
            EventHelper.CreateChatMessageEvent(
                agent,
                segment,
                requestId,
                model,
                null,

                "user",
                0,
                completionId,
                false,
                VendorName);
        }
    }

    public static class LLMDictionaryHelper
    {
        public static IDictionary<string, string> GetOpenAiHeaders(this IDictionary<string, string> headers)
        {
            var llmHeaders = new Dictionary<string, string>();
            foreach (var header in headers)
            {
                switch (header.Key)
                {
                    case "openai-version":
                        llmHeaders.Add("llmVersion", header.Value);
                        break;
                    case "x-ratelimit-limit-requests:":
                        llmHeaders.Add("ratelimitLimitRequests", header.Value);
                        break;
                    case "x-ratelimit-limit-tokens":
                        llmHeaders.Add("ratelimitLimitTokens", header.Value);
                        break;
                    case "x-ratelimit-remaining-requests":
                        llmHeaders.Add("ratelimitRemainingRequests", header.Value);
                        break;
                    case "x-ratelimit-remaining-tokens":
                        llmHeaders.Add("ratelimitRemainingTokens", header.Value);
                        break;
                    case "x-ratelimit-reset-requests":
                        llmHeaders.Add("ratelimitResetRequests", header.Value);
                        break;
                    case "x-ratelimit-reset-tokens":
                        llmHeaders.Add("ratelimitResetTokens", header.Value);
                        break;
                }
            }

            return llmHeaders;
        }

        public static string TryGetOpenAiOrganization(this IDictionary<string, string> headers)
        {
            if (headers.TryGetValue("openai-organization", out var organization))
            {
                return organization;
            }

            return null;
        }
    }
}
