// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
        public void CreateChatCompletionEvent_ShouldRecordLlmChatCompletionSummaryEvent()
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

            var headers = new Dictionary<string, string>
            {
                {"llmVersion", "1.0"},
                {"ratelimitLimitRequests", "99"},
                {"ratelimitLimitTokens", "99"},
                {"ratelimitResetTokens", "ratelimitResetTokens"},
                {"ratelimitResetRequests", "ratelimitResetRequests" },
                {"ratelimitRemainingTokens", "99"},
                {"ratelimitRemainingRequests", "99"},
                {"ratelimitLimitTokensUsageBased", "99"},
                {"ratelimitResetTokensUsageBased", "ratelimitResetTokensUsageBased"},
                {"ratelimitRemainingTokensUsageBased", "99"},
                {"header1", "value1"},
                {"header2", "value2"}
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
            var completionId = EventHelper.CreateChatCompletionEvent(_agent, _segment, requestId, temperature, maxTokens, requestModel, responseModel, numMessages, finishReason, vendor, false, headers, null);

            // Assert
            Assert.That(completionId, Is.Not.Null);

            // assert that _agent.RecordLlmEvent was called one time
            Mock.Assert(() => _agent.RecordLlmEvent("LlmChatCompletionSummary", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                // assert that the attributes passed to _agent.RecordLlmEvent are correct
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.Count, Is.EqualTo(23));
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
                Assert.That(llmAttributes["llmVersion"], Is.EqualTo("1.0"));
                Assert.That(llmAttributes["ratelimitLimitRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitLimitTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitResetTokens"], Is.EqualTo("ratelimitResetTokens"));
                Assert.That(llmAttributes["ratelimitResetRequests"], Is.EqualTo("ratelimitResetRequests"));
                Assert.That(llmAttributes["ratelimitRemainingTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitRemainingRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitLimitTokensUsageBased"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitResetTokensUsageBased"], Is.EqualTo("ratelimitResetTokensUsageBased"));
                Assert.That(llmAttributes["ratelimitRemainingTokensUsageBased"], Is.EqualTo(99));

                Assert.That(llmAttributes, Does.Not.ContainKey("error"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header1"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header2"));
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

        [TestCase(false)]
        [TestCase(true)]
        [Test]
        public void CreateChatMessageEvent_ShouldRecordLlmChatCompletionEvent(bool isResponse)
        {
            // Arrange
            var requestId = "123";
            var responseModel = "model1";
            var content = "Hello";
            var sequence = 1;
            var completionId = "456";
            var role = "role";

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
            EventHelper.CreateChatMessageEvent(_agent, _segment, requestId, responseModel, content, role, sequence, completionId, isResponse);

            // Assert
            Mock.Assert(() => _agent.RecordLlmEvent("LlmChatCompletionMessage", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                // assert that the attributes passed to _agent.RecordLlmEvent are correct
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.Count, Is.EqualTo(isResponse ? 12 : 11));
                Assert.That(llmAttributes["id"], Is.EqualTo(requestId + "-" + sequence));
                Assert.That(llmAttributes["request_id"], Is.EqualTo(requestId));
                Assert.That(llmAttributes["span_id"], Is.EqualTo(_segment.SpanId));
                Assert.That(llmAttributes["trace_id"], Is.EqualTo(_agent.GetLinkingMetadata()["trace.id"]));
                Assert.That(llmAttributes["response.model"], Is.EqualTo(responseModel));
                Assert.That(llmAttributes["vendor"], Is.EqualTo("bedrock"));
                Assert.That(llmAttributes["ingest_source"], Is.EqualTo("DotNet"));
                Assert.That(llmAttributes["content"], Is.EqualTo(content));
                Assert.That(llmAttributes["role"], Is.EqualTo(role));
                Assert.That(llmAttributes["sequence"], Is.EqualTo(sequence));
                Assert.That(llmAttributes["completion_id"], Is.EqualTo(completionId));
            });

            if (isResponse)
                Assert.That(llmAttributes["is_response"], Is.True);
            else
                Assert.That(llmAttributes, Does.Not.ContainKey("is_response"));

        }

        [Test]
        public void CreateEmbeddingEvent_ShouldRecordLlmEmbeddingEvent()
        {
            // Arrange
            var requestId = "123";
            var input = "input1";
            var requestModel = "model1";
            var responseModel = "model2";
            var vendor = "vendor1";

            var headers = new Dictionary<string, string>
            {
                {"llmVersion", "1.0"},
                {"ratelimitLimitRequests", "99"},
                {"ratelimitLimitTokens", "99"},
                {"ratelimitResetTokens", "ratelimitResetTokens"},
                {"ratelimitResetRequests", "ratelimitResetRequests" },
                {"ratelimitRemainingTokens", "99"},
                {"ratelimitRemainingRequests", "99"},
                {"ratelimitLimitTokensUsageBased", "99"},
                {"ratelimitResetTokensUsageBased", "ratelimitResetTokensUsageBased"},
                {"ratelimitRemainingTokensUsageBased", "99"},
                {"header1", "value1"},
                {"header2", "value2"}
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
            EventHelper.CreateEmbeddingEvent(_agent, _segment, requestId, input, requestModel, responseModel, vendor, false, headers, null);

            // Assert
            Mock.Assert(() => _agent.RecordLlmEvent("LlmEmbedding", Arg.IsAny<Dictionary<string, object>>()), Occurs.Once());

            Assert.Multiple(() =>
            {
                Assert.That(llmAttributes, Is.Not.Null);
                Assert.That(llmAttributes.Count, Is.EqualTo(20));
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
                Assert.That(llmAttributes["llmVersion"], Is.EqualTo("1.0"));

                Assert.That(llmAttributes["ratelimitLimitRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitLimitTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitResetTokens"], Is.EqualTo("ratelimitResetTokens"));
                Assert.That(llmAttributes["ratelimitResetRequests"], Is.EqualTo("ratelimitResetRequests"));
                Assert.That(llmAttributes["ratelimitRemainingTokens"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitRemainingRequests"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitLimitTokensUsageBased"], Is.EqualTo(99));
                Assert.That(llmAttributes["ratelimitResetTokensUsageBased"], Is.EqualTo("ratelimitResetTokensUsageBased"));
                Assert.That(llmAttributes["ratelimitRemainingTokensUsageBased"], Is.EqualTo(99));

                Assert.That(llmAttributes, Does.Not.ContainKey("error"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header1"));
                Assert.That(llmAttributes, Does.Not.ContainKey("header2"));

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
