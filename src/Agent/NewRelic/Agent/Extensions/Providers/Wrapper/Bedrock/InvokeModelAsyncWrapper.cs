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
using NewRelic.Core.JsonConverters;
using NewRelic.Core.JsonConverters.BedrockPayloads;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class InvokeModelAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

        private static ConcurrentDictionary<Type, Func<object, object>> _getResultFromGenericTask = new();
        private static ConcurrentDictionary<string, string> _libraryVersions  = new();
        private const string WrapperName = "BedrockInvokeModelAsync";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();
                transaction.DetachFromPrimary(); //Remove from thread-local type storage
            }

            dynamic invokeModelRequest = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var operationType = invokeModelRequest.ModelId.Contains("embed") ? "embedding" : "completion";
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/" + operationType + "/Bedrock/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            // required per spec
            var version = GetOrAddLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/Bedrock/" + version);

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
            if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    invokeModelRequest.ModelId,
                    "bedrock",
                    responsePayload.PromptTokenCount,
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
                "bedrock",
                false,
                null,  // not available in AWS
                null
            );

            // Prompt
            EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    invokeModelRequest.ModelId,
                    requestPayload.Prompt,
                    string.Empty,
                    0,
                    completionId,
                    responsePayload.PromptTokenCount,
                    false
                );

            // Responses
            for (var i = 0; i < responsePayload.Responses.Length; i++)
            {
                EventHelper.CreateChatMessageEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    invokeModelRequest.ModelId,
                    responsePayload.Responses[i].Content,
                    string.Empty,
                    i + 1,
                    completionId,
                    responsePayload.Responses[i].TokenCount,
                    true);
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

            if (((string)invokeModelRequest.ModelId).FromModelId() == LlmModelType.TitanEmbedded)
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    null,
                    "bedrock",
                    null,
                    true,
                    null,
                    errorData);
            }
            else
            {
                EventHelper.CreateChatCompletionEvent(
                    agent,
                    segment,
                    requestId,
                    requestPayload.Temperature,
                    requestPayload.MaxTokens,
                    invokeModelRequest.ModelId,
                    null,
                    0,
                    null,
                    "bedrock",
                    true,
                    null,
                    errorData);

            }
        }

        private static IRequestPayload GetRequestPayload(dynamic invokeModelRequest)
        {
            var model = ((string)invokeModelRequest.ModelId).FromModelId();
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
                case LlmModelType.Llama2:
                    return BedrockHelpers.DeserializeObject<Llama2RequestPayload>(utf8Json);
                case LlmModelType.CohereCommand:
                    return BedrockHelpers.DeserializeObject<CohereCommandRequestPayload>(utf8Json);
                case LlmModelType.Claude:
                    return BedrockHelpers.DeserializeObject<ClaudeRequestPayload>(utf8Json);
                case LlmModelType.Titan:
                case LlmModelType.TitanEmbedded:
                    return BedrockHelpers.DeserializeObject<TitanRequestPayload>(utf8Json);
                case LlmModelType.Jurassic:
                    return BedrockHelpers.DeserializeObject<JurassicRequestPayload>(utf8Json);
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unexpected LlmModelType");
            }
        }

        private static IResponsePayload GetResponsePayload(string modelId, dynamic invokeModelResponse)
        {
            var model = modelId.FromModelId();
            MemoryStream bodyStream = invokeModelResponse.Body;
            var curPos = bodyStream.Position;
            bodyStream.Position = 0;
            var sr = new StreamReader(bodyStream);
            string utf8Json = sr.ReadToEnd();
            bodyStream.Position = curPos;

            switch (model)
            {
                case LlmModelType.Llama2:
                    return BedrockHelpers.DeserializeObject<Llama2ResponsePayload>(utf8Json);
                case LlmModelType.CohereCommand:
                    return BedrockHelpers.DeserializeObject<CohereCommandResponsePayload>(utf8Json);
                case LlmModelType.Claude:
                    return BedrockHelpers.DeserializeObject<ClaudeResponsePayload>(utf8Json);
                case LlmModelType.Titan:
                    return BedrockHelpers.DeserializeObject<TitanResponsePayload>(utf8Json);
                case LlmModelType.TitanEmbedded:
                    return BedrockHelpers.DeserializeObject<TitanEmbeddedResponsePayload>(utf8Json);
                case LlmModelType.Jurassic:
                    return BedrockHelpers.DeserializeObject<JurassicResponsePayload>(utf8Json);
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model, "Unexpected LlmModelType");
            }
        }
    }

    /// <summary>
    /// The set of models supported by the Bedrock wrapper.
    /// </summary>
    public enum LlmModelType
    {
        Llama2,
        CohereCommand,
        Claude,
        Titan,
        TitanEmbedded,
        Jurassic
    }

    public static class LlmModelTypeExtensions
    {
        /// <summary>
        /// Converts a modelId to an LlmModelType. Throws an exception if the modelId is unknown.
        /// </summary>
        /// <param name="modelId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static LlmModelType FromModelId(this string modelId)
        {
            if (modelId.StartsWith("meta.llama2"))
                return LlmModelType.Llama2;

            if (modelId.StartsWith("cohere.command"))
                return LlmModelType.CohereCommand;

            if (modelId.StartsWith("anthropic.claude"))
                return LlmModelType.Claude;

            if (modelId.StartsWith("amazon.titan-text"))
                return LlmModelType.Titan;

            if (modelId.StartsWith("amazon.titan-embed-text"))
                return LlmModelType.TitanEmbedded;

            if (modelId.StartsWith("ai21.j2"))
                return LlmModelType.Jurassic;

            throw new Exception($"Unknown model: {modelId}");
        }
    }
}
