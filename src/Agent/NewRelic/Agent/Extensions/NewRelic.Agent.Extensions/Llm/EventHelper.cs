// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Llm
{
    public static class EventHelper
    {
        /// <summary>
        /// Creates a LlmChatCompletionSummary event.
        /// </summary>
        public static string CreateChatCompletionEvent(IAgent agent,
            ISegment segment,
            string requestId,
            float? temperature,
            int? maxTokens,
            string requestModel,
            string responseModel,
            int numMessages,
            string finishReason,
            string vendor,
            bool isError,
            IDictionary<string, string> headers,
            LlmErrorData errorData,
            string organization = null)
        {
            var completionId = Guid.NewGuid().ToString();

            var attributes = new Dictionary<string, object>
            {
                { "id", completionId },
                { "request_id", requestId },
                { "span_id", segment.SpanId },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                { "request.model", requestModel },
                { "response.model", responseModel },
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

            if (temperature.HasValue)
            {
                attributes.Add("request.temperature", temperature);
            }
            if (maxTokens.HasValue)
            {
                attributes.Add("request.max_tokens", maxTokens);
            }

            // LLM Metadata
            if (headers != null)
            {
                AddHeaderAttributes(headers, attributes);
            }

            if (!string.IsNullOrEmpty(organization))
            {
                attributes.Add("response.organization", organization);
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

        /// <summary>
        /// Creates a LlmChatCompletionMessage event.
        /// </summary>
        public static void CreateChatMessageEvent(IAgent agent,
            ISegment segment,
            string requestId,
            string responseId,
            string responseModel,
            string content,
            string role,
            int sequence,
            string completionId,
            bool isResponse,
            string vendor,
            int? tokenCount = null)
        {
            var attributes = new Dictionary<string, object>
            {
                { "id", string.IsNullOrEmpty(responseId) ? Guid.NewGuid().ToString() : (responseId + "-" + sequence) },
                { "request_id", requestId },
                { "span_id", segment.SpanId },
                { "trace_id", agent.GetLinkingMetadata()["trace.id"] },
                { "response.model", responseModel },
                { "vendor", vendor },
                { "ingest_source", "DotNet" },
                { "content", content },
                { "role", role },
                { "sequence", sequence },
                { "completion_id", completionId },
                //{ "llm.<user_defined_metadata>", "Pulled from Transaction metadata in RecordLlmEvent" },
            };

            if (tokenCount.HasValue)
            {
                attributes.Add("token_count", tokenCount.Value);
            }

            if (isResponse)
            {
                attributes.Add("is_response", true);
            }

            agent.RecordLlmEvent("LlmChatCompletionMessage", attributes);
        }

        /// <summary>
        /// Creates a LlmEmbedding event.
        /// </summary>
        public static void CreateEmbeddingEvent(IAgent agent,
            ISegment segment,
            string requestId,
            string input,
            string requestModel,
            string responseModel,
            string vendor,
            bool isError,
            IDictionary<string, string> headers,
            LlmErrorData errorData,
            string organization = null)
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

            if (!string.IsNullOrEmpty(organization))
            {
                attributes.Add("response.organization", organization);
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
            TryAddHeaderAttribute<string>(LLMConstants.Headers.LlmVersion);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitLimitRequests);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitLimitTokens);
            TryAddHeaderAttribute<string>(LLMConstants.Headers.RateLimitResetTokens);
            TryAddHeaderAttribute<string>(LLMConstants.Headers.RateLimitResetRequests);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitRemainingTokens);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitRemainingRequests);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitLimitTokensUsageBased);
            TryAddHeaderAttribute<string>(LLMConstants.Headers.RateLimitResetTokensUsageBased);
            TryAddHeaderAttribute<int>(LLMConstants.Headers.RateLimitRemainingTokensUsageBased);

            void TryAddHeaderAttribute<T>(string name)
            {
                var headerName = "response.headers." + name;
                if (!headers.TryGetValue(name, out var value))
                {
                    return;
                }

                if (typeof(T) == typeof(int))
                {
                    attributes.Add(headerName, Convert.ToInt32(value));
                }
                else // let it be a string
                {
                    attributes.Add(headerName, value);
                }
            }
        }
    }
}
