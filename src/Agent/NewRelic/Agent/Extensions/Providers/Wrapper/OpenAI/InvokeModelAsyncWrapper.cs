// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Net;
using System.IO;
using NewRelic.Agent.Extensions.JsonConverters;
using NewRelic.Agent.Extensions.JsonConverters.OpenAIPayloads;

namespace NewRelic.Providers.Wrapper.OpenAI
{
    public class InvokeModelAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
        private static ConcurrentDictionary<string, string> _libraryVersions = new();
        private const string WrapperName = "OpenAIInvokeModelAsync";
        private const string VendorName = "OpenAI";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
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

            dynamic invokeModelRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var operationType = "completion";// invokeModelRequest.ModelId.Contains("embed") ? "embedding" : "completion";
            var methodMethodName = $"Llm/{operationType}/{VendorName}/{instrumentedMethodCall.MethodCall.Method.MethodName}";
            var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, methodMethodName);

            // required per spec
            var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric($"DotNet/ML/{VendorName}/{version}");

            return Delegates.GetAsyncDelegateFor<Task>(
                agent,
                segment,
                false,
                InvokeTryProcessResponse,
                TaskContinuationOptions.ExecuteSynchronously
            );

            void InvokeTryProcessResponse(Task responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    HandleError(segment, invokeModelRequest, responseTask, agent);
                    segment.End();
                    return;
                }

                // We need the duration, so we end the segment before creating the events.
                segment.End();

                dynamic invokeModelResponse = GetTaskResult(responseTask);
                if (invokeModelResponse == null)// || invokeModelResponse.HttpStatusCode >= HttpStatusCode.MultipleChoices)
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model 'completion': Response payload is null");
                    return;
                }

                ProcessInvokeModel(segment, invokeModelRequest, invokeModelResponse, agent);
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

            var getResponse = _getResultFromGenericTask.GetOrAdd(task.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
            return getResponse(task);
        }


        private void ProcessInvokeModel(ISegment segment, dynamic invokeModelRequest, dynamic invokeModelResponse, IAgent agent)
        {
            var requestPayload = GetRequestPayload(invokeModelRequest, agent);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Could not deserialize request payload");
                return;
            }

            var responsePayload = GetResponsePayload("completion", invokeModelResponse, agent);
            if (responsePayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelResponse}: Could not deserialize response payload");
                return;
            }

            // Embedding - does not create the other events
            /*if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    invokeModelRequest.ModelId,
                    VendorName,
                    false,
                    null, // not available in AWS
                    null
                );

                return;
            }*/

            string finishReason = "";
            if (responsePayload.Choices.Length > 0)
            {
                finishReason = responsePayload.Choices[0].FinishReason;
            }

            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                responsePayload.Id,
                0,//requestPayload.Temperature,
                responsePayload.Usage.TotalTokens,
                requestPayload.Model,
                responsePayload.Model,
                1 + responsePayload.Choices.Length,
                finishReason,
                VendorName,
                false,
                null,
                null
            );

            // Prompt
            EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    responsePayload.Id,
                    responsePayload.Model,
                    requestPayload.Messages[0].Content,
                    requestPayload.Messages[0].Role,
                    0,
                    completionId,
                    false,
                    VendorName);

            // Responses
            for (var i = 0; i < responsePayload.Choices.Length; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    responsePayload.Id,
                    responsePayload.Model,//invokeModelRequest.ModelId,
                    responsePayload.Choices[i].Message.Content,
                    responsePayload.Choices[i].Message.Role,
                    i + 1,
                    completionId,
                    true,
                    VendorName);
            }
        }

        private void HandleError(ISegment segment, dynamic invokeModelRequest, Task responseTask, IAgent agent)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Error invoking OpenAI model {invokeModelRequest}: {responseTask.Exception!.Message}");

            dynamic oaiException = responseTask.Exception!.InnerException;
            if (oaiException == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Task faulted but there was no inner exception");
                return;
            }

            var requestPayload = GetRequestPayload(invokeModelRequest, agent);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest}: Could not deserialize request payload");
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

            /*if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    null,
                    VendorName,
                    true,
                    null,
                    errorData);
            }
            else
            {*/
            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                requestId,
                0,//requestPayload.Temperature,
                0,//requestPayload.MaxTokens,
                requestPayload.Model,
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
                    requestPayload.Model,
                    requestPayload.Messages[0].Content,
                    "user",
                    0,
                    completionId,
                    false,
                    VendorName);
            //}
        }

        private static IRequestPayload GetRequestPayload(dynamic invokeModelRequest, IAgent agent)
        {
            if (invokeModelRequest.ToString().Contains("OpenAI.Chat.ChatCompletionOptions"))
            {
                MemoryStream bodyStream = new MemoryStream();
                invokeModelRequest.WriteTo(bodyStream);
                var curPos = bodyStream.Position;
                bodyStream.Position = 0;
                var sr = new StreamReader(bodyStream);
                string utf8Json = sr.ReadToEnd();
                bodyStream.Position = curPos;
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 1: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTRequestPayload>(utf8Json);
            }
            /*else if (invokeModelRequest.ToString() == "System.ClientModel.BinaryContent")
             {
                 dynamic requestContent = invokeModelRequest.Data;
                 string utf8Json = requestContent.ToString();
                 agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 2: {utf8Json}");

                 rp.Model = model + "BinaryContent";
                 return null;
             }
             else if (invokeModelRequest.ToString() == "OpenAI.Chat.ChatMessage[]")
             {
                 rp.Model = model + "ChatMessage";
                 dynamic requestContent = invokeModelRequest;
                 string utf8Json = requestContent.ToString();
                 agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"request utf8Json 3: {utf8Json}");
                 return null;
             }*/

            return null;
        }

        private static IResponsePayload GetResponsePayload(string modelId, dynamic invokeModelResponse, IAgent agent)
        {
            GPTResponsePayload rp = new GPTResponsePayload();

            if (invokeModelResponse.ToString().Contains("OpenAI.Chat.ChatCompletion"))
            {
                dynamic responseContent = invokeModelResponse.GetRawResponse().Content;
                string utf8Json = responseContent.ToString();
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"response utf8Json: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTResponsePayload>(utf8Json);
            }
            else if (invokeModelResponse.ToString() == "System.ClientModel.ClientResult")
            {
                dynamic responseContent = invokeModelResponse.GetRawResponse().Content;
                string utf8Json = responseContent.ToString();
                agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"response utf8Json: {utf8Json}");

                return WrapperHelpers.DeserializeObject<GPTResponsePayload>(utf8Json);
            }

            return null;
        }
    }
}
