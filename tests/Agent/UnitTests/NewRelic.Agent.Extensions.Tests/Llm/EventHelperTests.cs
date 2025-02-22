// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Llm;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Llm
{
    [TestFixture]
    public class EventHelperTests
    {
        private IAgent _agent;
        private ISegment _segment;

        [SetUp]
        public void Setup()
        {
            // create a mock agent and segment using justmock
            _agent = Mock.Create<IAgent>();
            _segment = Mock.Create<ISegment>();
        }

        [Test]
        [TestCase(null, null, null)]
        [TestCase(100, 0.5f, "organization")]
        public void CreateChatCompletionEvent_ShouldRecordLlmChatCompletionSummaryEvent(int? maxTokens, float? temperature, string organization)
        {
            // Arrange
            var requestId = "123";
            var requestModel = "model1";
            var responseModel = "model2";
            var numMessages = 5;
            var finishReason = "completed";
            var vendor = "vendor1";

            var headers = new Dictionary<string, string>
            {
                { LLMConstants.Headers.LlmVersion, "1.0" },
                { LLMConstants.Headers.RateLimitLimitRequests, "99" },
                { LLMConstants.Headers.RateLimitLimitTokens, "99" },
                { LLMConstants.Headers.RateLimitResetTokens, "ratelimitResetTokens" },
                { LLMConstants.Headers.RateLimitResetRequests, "ratelimitResetRequests" },
                { LLMConstants.Headers.RateLimitRemainingTokens, "99" },
                { LLMConstants.Headers.RateLimitRemainingRequests, "99" },
                { LLMConstants.Headers.RateLimitLimitTokensUsageBased, "99" },
                { LLMConstants.Headers.RateLimitResetTokensUsageBased, "ratelimitResetTokensUsageBased" },
                { LLMConstants.Headers.RateLimitRemainingTokensUsageBased, "99" },
                { "header1", "value1" },
                { "header2", "value2" }
            };

            Mock.Arrange(() => _agent.GetLinkingMetadata()).Returns(
                new Dictionary<string, string>
                {
                    { "trace.id", "trace_id"}
                });

            // replace the RecordLlmEvent method with a mock so we can capture the attributes passed to it
            Dictionary<string, object> llmAttributes = null;
            Mock.Arrange(() => _agent.RecordLlmEvent("LlmChatCompletionSummary", Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string eventType, Dictionary<string, object> attributes) =>
                {
                    llmAttributes = attributes;
                });

            // Act
            var completionId = EventHelper.CreateChatCompletionEvent(_agent, _segment, requestId, temperature, maxTokens, requestModel, responseModel, numMessages, finishReason, vendor, false, headers, null, organization);

            // Assert
            Assert.That(completionId, Is.Not.Null);

            // assert that _agent.RecordLlmEvent was called one time
            Mock.Assert(() => _agent.RecordLlmEvent("LlmChatCompletionSummary", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            var expectedAttributeCount = 21;

            Assert.Multiple(() =>
            {
                // assert that the attributes passed to _agent.RecordLlmEvent are correct
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes["id"], Is.EqualTo(completionId));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["request.model"], Is.EqualTo(requestModel));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["response.number_of_messages"], Is.EqualTo(numMessages));
                Assert.That(llmAttributes["response.choices.finish_reason"], Is.EqualTo(finishReason));
                Assert.That(llmAttributes["vendor"], Is.EqualTo(vendor));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["duration"], Is.EqualTo((float)_segment.DurationOrZero.TotalMilliseconds));
                Assert.That(llmAttributes["response.headers.llmVersion"], Is.EqualTo("1.0"));
                Assert.That(llmAttributes["response.headers.ratelimitLimitRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitLimitTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitResetTokens"], Is.EqualTo("ratelimitResetTokens"));
                Assert.That(llmAttributes["response.headers.ratelimitResetRequests"], Is.EqualTo("ratelimitResetRequests"));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitLimitTokensUsageBased"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitResetTokensUsageBased"], Is.EqualTo("ratelimitResetTokensUsageBased"));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingTokensUsageBased"], Is.EqualTo(99));

                Assert.That(llmAttributes, Does.Not.ContainKey("error"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header1"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header2"));

                if (maxTokens.HasValue)
                {
                    Assert.That(llmAttributes["request.max_tokens"], Is.EqualTo(maxTokens));
                    ++expectedAttributeCount;
                }
                else
                {
                    Assert.That(llmAttributes, Does.Not.ContainKey("request.max_tokens"));
                }

                if (temperature.HasValue)
                {
                    Assert.That(llmAttributes["request.temperature"], Is.EqualTo(temperature));
                    ++expectedAttributeCount;
                }
                else
                {
                    Assert.That(llmAttributes, Does.Not.ContainKey("request.temperature"));
                }

                if (!string.IsNullOrEmpty(organization))
                {
                    Assert.That(llmAttributes["response.organization"], Is.EqualTo(organization));
                    ++expectedAttributeCount;
                }
                else
                {
                    Assert.That(llmAttributes, Does.Not.ContainKey("response.organization"));
                }

                Assert.That(llmAttributes.Count, Is.EqualTo(expectedAttributeCount));

            });
        }

        [Test]
        public void CreateChatCompletionEvent_ShouldNoticeError_WhenIsErrorIsTrue()
        {
            // Arrange
            var requestId = "123";
            var temperature = 98.6f;
            var maxTokens = 100;
            var requestModel = "model1";
            var responseModel = "model2";
            var numMessages = 5;
            var finishReason = "completed";
            var vendor = "vendor1";
            var errorData = new LlmErrorData
            {
                ErrorMessage = "error_message",
                ErrorCode = "error_code",
                ErrorParam = "error_param",
                HttpStatusCode = "500"
            };

            Mock.Arrange(() => _agent.GetLinkingMetadata()).Returns(
                new Dictionary<string, string>
                {
                    { "trace.id", "trace_id"}
                });

            // replace the RecordLlmEvent method with a mock so we can capture the attributes passed to it
            Dictionary<string, object> llmAttributes = null;
            Mock.Arrange(() => _agent.RecordLlmEvent("LlmChatCompletionSummary", Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string eventType, Dictionary<string, object> attributes) =>
                {
                    llmAttributes = attributes;
                });

            // create a mock agent api implementation
            var agentApiMock = Mock.Create<IAgentApi>();

            // replace NoticeError method with a mock so we can capture the error message and attributes
            var noticedError = false;
            string errorMessage = null;
            Dictionary<string, object> errorAttributes = null;

            Mock.Arrange(() => agentApiMock.NoticeError(Arg.IsAny<string>(), Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string message, Dictionary<string, object> attributes) =>
                {
                    noticedError = true;
                    errorMessage = message;
                    errorAttributes = attributes;
                });

            InternalApi.SetAgentApiImplementation(agentApiMock);

            // Act
            var completionId = EventHelper.CreateChatCompletionEvent(_agent, _segment, requestId, temperature, maxTokens, requestModel, responseModel, numMessages, finishReason, vendor, true, null, errorData);

            // Assert
            Assert.That(completionId, Is.Not.Null);

            // assert that _agent.RecordLlmEvent was called one time
            Mock.Assert(() => _agent.RecordLlmEvent("LlmChatCompletionSummary", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                // assert that the attributes passed to _agent.RecordLlmEvent are correct
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.Count, Is.EqualTo(14));
                Assert.That(llmAttributes["id"], Is.EqualTo(completionId));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["request.temperature"], Is.EqualTo(temperature));
                Assert.That(llmAttributes["request.max_tokens"], Is.EqualTo(maxTokens));
                Assert.That(llmAttributes["request.model"], Is.EqualTo(requestModel));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["response.number_of_messages"], Is.EqualTo(numMessages));
                Assert.That(llmAttributes["response.choices.finish_reason"], Is.EqualTo(finishReason));
                Assert.That(llmAttributes["vendor"], Is.EqualTo(vendor));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["duration"], Is.EqualTo((float)_segment.DurationOrZero.TotalMilliseconds));
                Assert.That(llmAttributes, Does.Not.ContainKey("llmVersion"));

                Assert.That(llmAttributes["error"], Is.True);

                Assert.That(noticedError, Is.True);
                Assert.That(errorMessage, Is.EqualTo(errorData.ErrorMessage));
                Assert.That(errorAttributes["error.code"], Is.EqualTo(errorData.ErrorCode));
                Assert.That(errorAttributes["error.param"], Is.EqualTo(errorData.ErrorParam));
                Assert.That(errorAttributes["http.statusCode"], Is.EqualTo(errorData.HttpStatusCode));
            });
        }

        [Test]
        [TestCase(false, null)]
        [TestCase(true, 123)]
        public void CreateChatMessageEvent_ShouldRecordLlmChatCompletionEvent(bool isResponse, int? tokenCount)
        {
            // Arrange
            var requestId = "123";
            var responseModel = "model1";
            var content = "Hello";
            var sequence = 1;
            var completionId = "456";
            var role = "role";
            var vendor = "vendor";

            Mock.Arrange(() => _agent.GetLinkingMetadata()).Returns(
            new Dictionary<string, string>
            {
                    { "trace.id", "trace_id"}
            });

            // replace the RecordLlmEvent method with a mock so we can capture the attributes passed to it
            Dictionary<string, object> llmAttributes = null;
            Mock.Arrange(() => _agent.RecordLlmEvent("LlmChatCompletionMessage", Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string eventType, Dictionary<string, object> attributes) =>
                {
                    llmAttributes = attributes;
                });

            // Act
            var responseId = Guid.NewGuid().ToString();
            EventHelper.CreateChatMessageEvent(_agent, _segment, requestId, responseId, responseModel, content, role, sequence, completionId, isResponse, vendor, tokenCount);


            var expectedAttributeCount = 11;

            // Assert
            Mock.Assert(() => _agent.RecordLlmEvent("LlmChatCompletionMessage", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                // assert that the attributes passed to _agent.RecordLlmEvent are correct
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes["id"], Is.EqualTo(responseId + "-" + sequence));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["vendor"], Is.EqualTo(vendor));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["content"], Is.EqualTo(content));
                Assert.That(llmAttributes["role"], Is.EqualTo(role));
                Assert.That(llmAttributes["sequence"], Is.EqualTo(sequence));
                Assert.That(llmAttributes["completion_id"], Is.EqualTo(completionId));

                if (isResponse)
                {
                    Assert.That(llmAttributes["is_response"], Is.True);
                    ++expectedAttributeCount;
                }
                else
                    Assert.That(llmAttributes, Does.Not.ContainKey("is_response"));

                if (tokenCount.HasValue)
                {
                    Assert.That(llmAttributes["token_count"], Is.EqualTo(tokenCount));
                    ++expectedAttributeCount;
                }
                else
                    Assert.That(llmAttributes, Does.Not.ContainKey("token_count"));

                Assert.That(llmAttributes.Count, Is.EqualTo(expectedAttributeCount));
            });

        }

        [Test]
        [TestCase("organization")]
        [TestCase(null)]

        public void CreateEmbeddingEvent_ShouldRecordLlmEmbeddingEvent(string organization)
        {
            // Arrange
            var requestId = "123";
            var input = "input1";
            var requestModel = "model1";
            var responseModel = "model2";
            var vendor = "vendor1";

            var headers = new Dictionary<string, string>
            {
                { LLMConstants.Headers.LlmVersion, "1.0" },
                { LLMConstants.Headers.RateLimitLimitRequests, "99" },
                { LLMConstants.Headers.RateLimitLimitTokens, "99" },
                { LLMConstants.Headers.RateLimitResetTokens, "ratelimitResetTokens" },
                { LLMConstants.Headers.RateLimitResetRequests, "ratelimitResetRequests" },
                { LLMConstants.Headers.RateLimitRemainingTokens, "99" },
                { LLMConstants.Headers.RateLimitRemainingRequests, "99" },
                { LLMConstants.Headers.RateLimitLimitTokensUsageBased, "99" },
                { LLMConstants.Headers.RateLimitResetTokensUsageBased, "ratelimitResetTokensUsageBased" },
                { LLMConstants.Headers.RateLimitRemainingTokensUsageBased, "99" },
                { "header1", "value1" },
                { "header2", "value2" }
            };

            Mock.Arrange(() => _agent.GetLinkingMetadata()).Returns(
                new Dictionary<string, string>
                {
                    { "trace.id", "trace_id"}
                });

            // replace the RecordLlmEvent method with a mock so we can capture the attributes passed to it
            Dictionary<string, object> llmAttributes = null;
            Mock.Arrange(() => _agent.RecordLlmEvent("LlmEmbedding", Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string eventType, Dictionary<string, object> attributes) =>
                {
                    llmAttributes = attributes;
                });

            // Act
            EventHelper.CreateEmbeddingEvent(_agent, _segment, requestId, input, requestModel, responseModel, vendor, false, headers, null, organization);

            // Assert
            Mock.Assert(() => _agent.RecordLlmEvent("LlmEmbedding", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            var expectedAttributeCount = 20;
            Assert.Multiple(() =>
            {
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.ContainsKey("id"));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["input"], Is.EqualTo(input));
                Assert.That(llmAttributes["request.model"], Is.EqualTo(requestModel));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["vendor"], Is.EqualTo(vendor));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["duration"], Is.EqualTo((float)_segment.DurationOrZero.TotalMilliseconds));
                Assert.That(llmAttributes["response.headers.llmVersion"], Is.EqualTo("1.0"));
                Assert.That(llmAttributes["response.headers.ratelimitLimitRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitLimitTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitResetTokens"], Is.EqualTo("ratelimitResetTokens"));
                Assert.That(llmAttributes["response.headers.ratelimitResetRequests"], Is.EqualTo("ratelimitResetRequests"));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitLimitTokensUsageBased"], Is.EqualTo(99));
                Assert.That(llmAttributes["response.headers.ratelimitResetTokensUsageBased"], Is.EqualTo("ratelimitResetTokensUsageBased"));
                Assert.That(llmAttributes["response.headers.ratelimitRemainingTokensUsageBased"], Is.EqualTo(99));

                if (!string.IsNullOrEmpty(organization))
                {
                    Assert.That(llmAttributes["response.organization"], Is.EqualTo(organization));
                    ++expectedAttributeCount;
                }
                else
                    Assert.That(llmAttributes, Does.Not.ContainKey("response.organization"));

                Assert.That(llmAttributes, Does.Not.ContainKey("error"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header1"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header2"));

                Assert.That(llmAttributes.Count, Is.EqualTo(expectedAttributeCount));
            });
        }

        [Test]
        public void CreateEmbeddingEvent_ShouldNoticeError_WhenIsErrorIsTrue()
        {
            // Arrange
            var requestId = "123";
            var input = "input1";
            var requestModel = "model1";
            var responseModel = "model2";
            var vendor = "vendor1";

            var errorData = new LlmErrorData
            {
                ErrorMessage = "error_message",
                ErrorCode = "error_code",
                ErrorParam = "error_param",
                HttpStatusCode = "500"
            };

            Mock.Arrange(() => _agent.GetLinkingMetadata()).Returns(
                new Dictionary<string, string>
                {
                    { "trace.id", "trace_id"}
                });

            // replace the RecordLlmEvent method with a mock so we can capture the attributes passed to it
            Dictionary<string, object> llmAttributes = null;
            Mock.Arrange(() => _agent.RecordLlmEvent("LlmEmbedding", Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string eventType, Dictionary<string, object> attributes) =>
                {
                    llmAttributes = attributes;
                });

            // create a mock agent api implementation
            var agentApiMock = Mock.Create<IAgentApi>();

            // replace NoticeError method with a mock so we can capture the error message and attributes
            var noticedError = false;
            string errorMessage = null;
            Dictionary<string, object> errorAttributes = null;

            Mock.Arrange(() => agentApiMock.NoticeError(Arg.IsAny<string>(), Arg.IsAny<Dictionary<string, object>>()))
                .DoInstead((string message, Dictionary<string, object> attributes) =>
                {
                    noticedError = true;
                    errorMessage = message;
                    errorAttributes = attributes;
                });

            InternalApi.SetAgentApiImplementation(agentApiMock);


            // Act
            EventHelper.CreateEmbeddingEvent(_agent, _segment, requestId, input, requestModel, responseModel, vendor, true, null, errorData);

            // Assert
            Mock.Assert(() => _agent.RecordLlmEvent("LlmEmbedding", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.Count, Is.EqualTo(11));
                Assert.That(llmAttributes.ContainsKey("id"));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["input"], Is.EqualTo(input));
                Assert.That(llmAttributes["request.model"], Is.EqualTo(requestModel));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["vendor"], Is.EqualTo(vendor));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["duration"], Is.EqualTo((float)_segment.DurationOrZero.TotalMilliseconds));

                Assert.That(llmAttributes, Does.Not.ContainKey("llmVersion"));

                Assert.That(llmAttributes["error"], Is.True);

                Assert.That(noticedError, Is.True);
                Assert.That(errorMessage, Is.EqualTo(errorData.ErrorMessage));
                Assert.That(errorAttributes["error.code"], Is.EqualTo(errorData.ErrorCode));
                Assert.That(errorAttributes["error.param"], Is.EqualTo(errorData.ErrorParam));
                Assert.That(errorAttributes["http.statusCode"], Is.EqualTo(errorData.HttpStatusCode));
            });
        }
    }
}
