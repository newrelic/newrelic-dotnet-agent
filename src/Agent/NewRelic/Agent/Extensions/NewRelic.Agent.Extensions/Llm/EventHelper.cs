// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Llm
{
    public static class EventHelper
    {
        public static string CreateChatCompletionEvent(IAgent agent,
            ISegment segment,
            string requestId,
            float temperature,
            int maxTokens,
            string requestModel,
            string responseModel,
            int numMessages,
            string finishReason,
            string vendor,
            bool isError,
            IDictionary<string, string> headers,
            LlmErrorData errorData)
        {
            var completionId = Guid.NewGuid().ToString();

            var attributes = new Dictionary<string, object>
            {
                { "id", completionId },
                { "request_id", requestId },
                { "span_id", segment.SpanId },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                { "request.temperature", temperature },
                { "request.max_tokens", maxTokens },
                { "request.model", requestModel },
                { "response.model", responseModel },
                //{ "response.organization", "not available" },
                { "response.number_of_messages", numMessages },
                { "response.choices.finish_reason", finishReason },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "duration", (float)segment.DurationOrZero.TotalMilliseconds },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
                //{ "response.headers.<vendor_specific_headers>", "See LLM headers below" },
            };

            if (isError)
            {
                attributes.Add("error", isError);
            }

            // LLM Metadata
            if (headers != null)
            {
                AddHeaderAttributes(headers, attributes);
            }

            agent.RecordLlmEvent("LlmChatCompletionSummary", attributes);

            if (isError)
            {
                var errorAttributes = new Dictionary<string, object>
                {
                    { "error.code", errorData.ErrorCode },
                    { "error.param", errorData.ErrorParam },
                    { "http.statusCode", errorData.HttpStatusCode },
                    { "completion_id", completionId }, 
                    //{ "embedding_id", embeddingId } not available for completion summary 
                };

                InternalApi.NoticeError(errorData.ErrorMessage, errorAttributes);
            }

            return completionId;
        }

        public static void CreateChatMessageEvent(IAgent agent,
            ISegment segment,
            string requestId,
            string responseModel,
            string content,
            string role,
            int sequence,
            string completionId,
            bool isResponse)
        {
            var attributes = new Dictionary<string, object>
            {
                { "id", requestId + "-" + sequence },
                { "request_id", requestId },
                { "span_id", segment.SpanId },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                { "response.model", responseModel },
                { "vendor", "bedrock" },
                { "ingest_source", "DotNet" },
                { "content", content },
                { "role", role },
                { "sequence", sequence },
                { "completion_id", completionId },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
            };

            if (isResponse)
            {
                attributes.Add("is_response", true);
            }

            agent.RecordLlmEvent("LlmChatCompletionMessage", attributes);
        }

        public static void CreateEmbeddingEvent(IAgent agent,
            ISegment segment,
            string requestId,
            string input,
            string requestModel,
            string responseModel,
            string vendor,
            bool isError,
            IDictionary<string, string> headers,
            LlmErrorData errorData)
        {
            var embeddingId = Guid.NewGuid().ToString();

            var attributes = new Dictionary<string, object>
            {
                { "id", embeddingId },
                { "request_id", requestId },
                { "span_id", segment.SpanId },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                { "input", input },
                { "request.model", requestModel },
                { "response.model", responseModel },
                //{ "response.organization", "not available" },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "duration", (float)segment.DurationOrZero.TotalMilliseconds },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
                //{ "response.headers.<vendor_specific_headers>", "See LLM headers below" },
            };

            if (isError)
            {
                attributes.Add("error", isError);
            }

            // LLM headers
            if (headers != null)
            {
                AddHeaderAttributes(headers, attributes);
            }

            agent.RecordLlmEvent("LlmEmbedding", attributes);

            if (isError)
            {
                var errorAttributes = new Dictionary<string, object>
                {
                    { "error.code", errorData.ErrorCode },
                    { "error.param", errorData.ErrorParam },
                    { "http.statusCode", errorData.HttpStatusCode },
                    //{ "completion_id", completionId }, not available for embedding
                    { "embedding_id", embeddingId } 
                };

                InternalApi.NoticeError(errorData.ErrorMessage, errorAttributes);
            }
        }

        private static void AddHeaderAttributes(IDictionary<string, string> headers, IDictionary<string, object> attributes)
        {
            TryAddHeaderAttribute<string>("llmVersion");
            TryAddHeaderAttribute<int>("ratelimitLimitRequests");
            TryAddHeaderAttribute<int>("ratelimitLimitTokens");
            TryAddHeaderAttribute<string>("ratelimitResetTokens");
            TryAddHeaderAttribute<string>("ratelimitResetRequests");
            TryAddHeaderAttribute<int>("ratelimitRemainingTokens");
            TryAddHeaderAttribute<int>("ratelimitRemainingRequests");
            TryAddHeaderAttribute<int>("ratelimitLimitTokensUsageBased");
            TryAddHeaderAttribute<string>("ratelimitResetTokensUsageBased");
            TryAddHeaderAttribute<int>("ratelimitRemainingTokensUsageBased");

            void TryAddHeaderAttribute<T>(string name)
            {
                if (!headers.TryGetValue(name, out var value))
                {
                    return;
                }

                if (typeof(T) == typeof(int))
                {
                    attributes.Add(name, Convert.ToInt32(value));
                }
                else // let it be a string
                {
                    attributes.Add(name, value);
                }
            }
        }
    }
}
