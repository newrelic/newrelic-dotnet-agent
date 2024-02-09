// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.BedrockRuntime.Model;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.Bedrock
{
    public static class Helpers
    {
        public static string CreateChatCompletionEvent(IAgent agent,
            ITransaction transaction,
            IRequestPayload requestPayload,
            IResponsePayload responsePayload,
            InvokeModelRequest invokeRequestModel,
            InvokeModelResponse invokeResponseModel)
        {
            var completionId = Guid.NewGuid().ToString();

            var attributes = new Dictionary<string, object>
            {
                { "id", completionId },
                { "request_id", invokeResponseModel.ResponseMetadata.RequestId },
                { "transaction_id", transaction.Guid },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                //{ "api_key_last_four_digits", "NOT_AVAILABLE" },
                { "request.temperature", requestPayload.Temperature },
                { "request.max_tokens", requestPayload.MaxTokens },
                { "request.model", invokeRequestModel.ModelId },
                { "response.model", invokeRequestModel.ModelId },
                //{ "response.organization", "NOT_AVAILABLE" },
                { "response.number_of_messages", "2" },
                { "response.usage.total_tokens", responsePayload.TotalTokenCount },
                { "response.usage.completion_tokens", responsePayload.CompletionTokenCount },
                { "response.usage.prompt_tokens", responsePayload.PromptTokenCount },
                { "response.choices.finish_reason", responsePayload.StopReason },
                { "vendor", "bedrock" },
                { "ingest_source", "DotNet" },
                //{ "response.duration", "NOT_AVAILABLE" },
                //{ "error", "NOT SURE WHERE WE COULD CAPTURE THIS" },
                //{ "llm.<user_defined_metadata>", "SEE FOREACH BELOW" },
                { "conversation_id", "NEW API" },
            };

            foreach (var data in invokeResponseModel.ResponseMetadata.Metadata)
            {
                attributes.Add("llm." + data.Key, data.Value);
            }

            agent.RecordLlmEvent("LlmChatCompletionSummary", attributes);

            return completionId;
        }

        public static void CreateChatMessageEvents(IAgent agent,
            string spandId,
            ITransaction transaction,
            string completionId,
            IRequestPayload requestPayload,
            IResponsePayload responsePayload,
            InvokeModelRequest invokeRequestModel,
            InvokeModelResponse invokeResponseModel)
        {
            // Prompt
            CreateChatMessageEvent(agent,
                spandId,
                transaction.Guid,
                agent.GetLinkingMetadata()["trace.id"],
                completionId,
                requestPayload.Prompt,
                0,
                false,
                invokeRequestModel,
                invokeResponseModel);

            // Responses
            for (var i = 0;i < responsePayload.Responses.Length;i++)
            {
                CreateChatMessageEvent(agent,
                    spandId,
                    transaction.Guid,
                    agent.GetLinkingMetadata()["trace.id"],
                    completionId,
                    responsePayload.Responses[i],
                    i + 1, // prompt is 0
                    true,
                    invokeRequestModel,
                    invokeResponseModel);
            }
        }

        private static void CreateChatMessageEvent(IAgent agent,
            string spanId,
            string transactionGuid,
            string traceId,
            string completionId,
            string message,
            int sequence,
            bool isResponse,
            InvokeModelRequest invokeRequestModel,
            InvokeModelResponse invokeResponseModel)
        {
            var attributes = new Dictionary<string, object>
            {
                { "id", invokeResponseModel.ResponseMetadata.RequestId + "-" + sequence},
                { "request_id", invokeResponseModel.ResponseMetadata.RequestId },
                { "span_id", spanId },
                { "transaction_id", transactionGuid },
                { "trace_id", traceId },
                { "conversation_id", "NEW API" },
                //{ "api_key_last_four_digits", "NOT_AVAILABLE" },
                { "response.model", invokeRequestModel.ModelId },
                { "vendor", "bedrock" },
                { "ingest_source", "DotNet" },
                { "content", message },
                //{ "role", "NOT_AVAILABLE" },
                { "sequence", sequence },
                { "completion_id", completionId },
                //{ "llm.<user_defined_metadata>", "SEE FOREACH BELOW" },
            };

            if (isResponse)
            {
                attributes.Add("is_response", true);
            }

            foreach (var data in invokeResponseModel.ResponseMetadata.Metadata)
            {
                attributes.Add("llm." + data.Key, data.Value);
            };

            agent.RecordLlmEvent("LlmChatCompletionMessage", attributes);
        }

        public static IRequestPayload GetRequestPayload(InvokeModelRequest invokeModelRequest)
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

            //if (invokeModelRequest.ModelId.StartsWith("amazon.titan-embed-text"))
            //{
            //    throw new NotImplementedException();
            //    //return JsonSerializer.Deserialize<TitanRequestPayload>(invokeModelRequest.Body.ToArray());
            //}

            if (invokeModelRequest.ModelId.StartsWith("ai21.j2"))
            {
                return JsonSerializer.Deserialize<JurassicRequestPayload>(invokeModelRequest.Body.ToArray());
            }

            return null;
        }

        public static IResponsePayload GetResponsePayload(string model, InvokeModelResponse invokeModelResponse)
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

            //if (model.StartsWith("amazon.titan-embed-text"))
            //{
            //    throw new NotImplementedException();
            //    //return JsonSerializer.Deserialize<TitanResponsePayload>(invokeModelResponse.Body.ToArray());
            //}

            if (model.StartsWith("ai21.j2"))
            {
                return JsonSerializer.Deserialize<JurassicResponsePayload>(invokeModelResponse.Body.ToArray());
            }

            return null;
        }
    }
}
