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
using NewRelic.Agent.Extensions.JsonConverters.BedrockPayloads;
using NewRelic.Agent.Extensions.Llm.Bedrock;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class InvokeModelAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
        private static ConcurrentDictionary<string, string> _libraryVersions  = new();
        private const string WrapperName = "BedrockInvokeModelAsync";
        private const string VendorName = "Bedrock";

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
            SupportabilityHelpers.CreateModelIdSupportabilityMetrics((string)invokeModelRequest.ModelId, agent);
            var operationType = invokeModelRequest.ModelId.Contains("embed") ? "embedding" : "completion";
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                $"Llm/{operationType}/{VendorName}/{instrumentedMethodCall.MethodCall.Method.MethodName}"
            );

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
                if (invokeModelResponse == null || invokeModelResponse.HttpStatusCode >= HttpStatusCode.MultipleChoices)
                {
                    agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Response payload {(invokeModelResponse == null ? "is null" : $"has non-success HttpStatusCode: {invokeModelResponse.HttpStatusCode}")}");
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
            var requestPayload = GetRequestPayload(invokeModelRequest);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Could not deserialize request payload");
                return;
            }

            var responsePayload = GetResponsePayload(invokeModelRequest.ModelId, invokeModelResponse);
            if (responsePayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Could not deserialize response payload");
                return;
            }

            // Embedding - does not create the other events
            if (((string)invokeModelRequest.ModelId).FromBedrockModelId() == BedrockLlmModelType.TitanEmbedded)
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
            }

            var completionId = EventHelper.CreateChatCompletionEvent(
                agent,
                segment,
                invokeModelResponse.ResponseMetadata.RequestId,
                requestPayload.Temperature,
                requestPayload.MaxTokens,
                invokeModelRequest.ModelId,
                invokeModelRequest.ModelId,
                1 + responsePayload.Responses.Length,
                responsePayload.StopReason,
                VendorName,
                false,
                null,  // not available in AWS
                null
            );

            // Prompt
            EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    null,
                    invokeModelRequest.ModelId,
                    requestPayload.Prompt,
                    "user",
                    0,
                    completionId,
                    false,
                    VendorName);

            // Responses
            for (var i = 0; i < responsePayload.Responses.Length; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    null,
                    invokeModelRequest.ModelId,
                    responsePayload.Responses[i].Content,
                    "assistant",
                    i + 1,
                    completionId,
                    true,
                    VendorName);
            }
        }

        private void HandleError(ISegment segment, dynamic invokeModelRequest, Task responseTask, IAgent agent)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Error invoking Bedrock model {invokeModelRequest.ModelId}: {responseTask.Exception!.Message}");

            dynamic bedrockException = responseTask.Exception!.InnerException;
            if (bedrockException == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Task faulted but there was no inner exception");
                return;
            }

            var requestPayload = GetRequestPayload(invokeModelRequest);
            if (requestPayload == null)
            {
                agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, $"Error invoking model {invokeModelRequest.ModelId}: Could not deserialize request payload");
                return;
            }

            HttpStatusCode statusCode = bedrockException.StatusCode;
            string errorCode = bedrockException.ErrorCode;
            string errorMessage = bedrockException.Message;
            string requestId = bedrockException.RequestId;

            var errorData = new LlmErrorData
            {
                HttpStatusCode = ((int)statusCode).ToString(),
                ErrorCode = errorCode,
                ErrorParam = null, // not available in AWS
                ErrorMessage = errorMessage
            };

            if (((string)invokeModelRequest.ModelId).FromBedrockModelId() == BedrockLlmModelType.TitanEmbedded)
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
            {
                var completionId = EventHelper.CreateChatCompletionEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Temperature,
                    requestPayload.MaxTokens,
                    invokeModelRequest.ModelId,
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
                        null,
                        invokeModelRequest.ModelId,
                        requestPayload.Prompt,
                        "user",
                        0,
                        completionId,
                        false,
                        VendorName);
            }
        }

        private static IRequestPayload GetRequestPayload(dynamic invokeModelRequest)
        {
            var model = ((string)invokeModelRequest.ModelId).FromBedrockModelId();
            MemoryStream bodyStream = invokeModelRequest.Body;
            var curPos = bodyStream.Position;
            bodyStream.Position = 0;
            var sr = new StreamReader(bodyStream);
            string utf8Json = sr.ReadToEnd();
            bodyStream.Position = curPos;

            switch (model)
            {
                // We're using a helper method in NewRelic.Core because it has Newtonsoft.Json ILRepacked into it
                // This avoids depending on Newtonsoft.Json being available in the customer application
                case BedrockLlmModelType.Llama2:
                    return WrapperHelpers.DeserializeObject<Llama2RequestPayload>(utf8Json);
                case BedrockLlmModelType.CohereCommand:
                    return WrapperHelpers.DeserializeObject<CohereCommandRequestPayload>(utf8Json);
                case BedrockLlmModelType.Claude:
                    return WrapperHelpers.DeserializeObject<ClaudeRequestPayload>(utf8Json);
                case BedrockLlmModelType.Titan:
                case BedrockLlmModelType.TitanEmbedded:
                    return WrapperHelpers.DeserializeObject<TitanRequestPayload>(utf8Json);
                case BedrockLlmModelType.Jurassic:
                    return WrapperHelpers.DeserializeObject<JurassicRequestPayload>(utf8Json);
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unexpected LlmModelType");
            }
        }

        private static IResponsePayload GetResponsePayload(string modelId, dynamic invokeModelResponse)
        {
            var model = modelId.FromBedrockModelId();
            MemoryStream bodyStream = invokeModelResponse.Body;
            var curPos = bodyStream.Position;
            bodyStream.Position = 0;
            var sr = new StreamReader(bodyStream);
            string utf8Json = sr.ReadToEnd();
            bodyStream.Position = curPos;

            switch (model)
            {
                case BedrockLlmModelType.Llama2:
                    return WrapperHelpers.DeserializeObject<Llama2ResponsePayload>(utf8Json);
                case BedrockLlmModelType.CohereCommand:
                    return WrapperHelpers.DeserializeObject<CohereCommandResponsePayload>(utf8Json);
                case BedrockLlmModelType.Claude:
                    return WrapperHelpers.DeserializeObject<ClaudeResponsePayload>(utf8Json);
                case BedrockLlmModelType.Titan:
                    return WrapperHelpers.DeserializeObject<TitanResponsePayload>(utf8Json);
                case BedrockLlmModelType.TitanEmbedded:
                    return WrapperHelpers.DeserializeObject<TitanEmbeddedResponsePayload>(utf8Json);
                case BedrockLlmModelType.Jurassic:
                    return WrapperHelpers.DeserializeObject<JurassicResponsePayload>(utf8Json);
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unexpected LlmModelType");
            }
        }
    }
}
