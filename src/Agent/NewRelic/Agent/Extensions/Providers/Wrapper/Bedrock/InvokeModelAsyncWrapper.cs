// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Threading.Tasks;
using Amazon.BedrockRuntime.Model;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Llm;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Providers.Wrapper.Bedrock.Payloads;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public class InvokeModelAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true; // part of spec, only create events for transactions.

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

            var invokeModelRequest = (InvokeModelRequest)instrumentedMethodCall.MethodCall.MethodArguments[0];
            var operationType = invokeModelRequest.ModelId.Contains("embed") ? "embedding" : "completion";
            var segment = transaction.StartCustomSegment(
                instrumentedMethodCall.MethodCall,
                "Llm/" + operationType + "/Bedrock/" + instrumentedMethodCall.MethodCall.Method.MethodName
            );

            // required per spec
            // TODO: persist the version string in a field to avoid repeated calls to GetLibraryVersion
            var version = VersionHelpers.GetLibraryVersion(instrumentedMethodCall.MethodCall.Method.Type.Assembly.ManifestModule.Assembly.FullName);
            agent.RecordSupportabilityMetric("DotNet/ML/Bedrock/" + version);
            
            return Delegates.GetAsyncDelegateFor<Task<InvokeModelResponse>>(
                agent,
                segment,
                false,
                InvokeTryProcessResponse,
                TaskContinuationOptions.RunContinuationsAsynchronously
            );

            void InvokeTryProcessResponse(Task<InvokeModelResponse> responseTask)
            {
                if (responseTask.IsFaulted)
                {
                    HandleError(segment, invokeModelRequest, responseTask, agent);
                }

                var invokeModelResponse = responseTask.Result;
                if (invokeModelResponse is not { HttpStatusCode: < System.Net.HttpStatusCode.MultipleChoices })
                {
                    // do something drastic?
                    segment.End();
                    return;
                }

                // We need the duration, so we end the segment before creating the events.
                segment.End();

                ProcessInvokeModel(segment, invokeModelRequest, invokeModelResponse, agent);
            }
        }

        private void ProcessInvokeModel(ISegment segment, InvokeModelRequest invokeModelRequest, InvokeModelResponse invokeModelResponse, IAgent agent)
        {
            var requestPayload = GetRequestPayload(invokeModelRequest);
            if (requestPayload == null)
            {
                return;
            }

            var responsePayload = GetResponsePayload(invokeModelRequest.ModelId, invokeModelResponse);
            if (responsePayload == null)
            {
                return;
            }

            // Embedding - does not create the other events
            if (invokeModelRequest.ModelId.StartsWith("amazon.titan-embed-text")) // might be changed to Contains("embed")...
            {
                EventHelper.CreateEmbeddingEvent(
                    agent,
                    segment,
                    invokeModelResponse.ResponseMetadata.RequestId,
                    requestPayload.Prompt,
                    invokeModelRequest.ModelId,
                    invokeModelRequest.ModelId,
                    "bedrock",
                    responsePayload.Responses[0].TokenCount,
                    false,
                    null // not available in AWS
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
                null // not available in AWS
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
                    true
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
                    true
                );
            }
        }

        private void HandleError(ISegment segment, InvokeModelRequest invokeModelRequest, Task<InvokeModelResponse> responseTask, IAgent agent)
        {
            //TODO: This is not fully fleshed out.  it is just a stub.
            agent.Logger.Log(Agent.Extensions.Logging.Level.Info, $"Error invoking model: {responseTask.Exception}");


            var requestPayload = GetRequestPayload(invokeModelRequest);
            if (requestPayload == null)
            {
                return;
            }

            IResponsePayload responsePayload = null;
            InvokeModelResponse invokeModelResponse = null;

            _= EventHelper.CreateChatCompletionEvent(
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
                null
            );
        }

        private static IRequestPayload GetRequestPayload(InvokeModelRequest invokeModelRequest)
        {
            if (invokeModelRequest.ModelId.StartsWith("meta.llama2"))
            {
                return JsonSerializer.Deserialize<Llama2RequestPayload>(invokeModelRequest.Body.ToArray());
            }

            if (invokeModelRequest.ModelId.StartsWith("cohere.command"))
            {
                return JsonSerializer.Deserialize<CohereCommandRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            if (invokeModelRequest.ModelId.StartsWith("anthropic.claude"))
            {
                return JsonSerializer.Deserialize<ClaudeRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            if (invokeModelRequest.ModelId.StartsWith("amazon.titan-text"))
            {
                return JsonSerializer.Deserialize<TitanRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            if (invokeModelRequest.ModelId.StartsWith("amazon.titan-embed-text"))
            {
                return JsonSerializer.Deserialize<TitanRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            if (invokeModelRequest.ModelId.StartsWith("ai21.j2"))
            {
                return JsonSerializer.Deserialize<JurassicRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            return null;
        }

        private static IResponsePayload GetResponsePayload(string model, InvokeModelResponse invokeModelResponse)
        {
            if (model.StartsWith("meta.llama2"))
            {
                return JsonSerializer.Deserialize<Llama2ResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            if (model.StartsWith("cohere.command"))
            {
                return JsonSerializer.Deserialize<CohereCommandResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            if (model.StartsWith("anthropic.claude"))
            {
                return JsonSerializer.Deserialize<ClaudeResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            if (model.StartsWith("amazon.titan-text"))
            {
                return JsonSerializer.Deserialize<TitanResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            if (model.StartsWith("amazon.titan-embed-text"))
            {
                return JsonSerializer.Deserialize<TitanResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            if (model.StartsWith("ai21.j2"))
            {
                return JsonSerializer.Deserialize<JurassicResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            return null;
        }
    }
}
