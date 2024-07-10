// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Amazon.SQS.Model;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    public class SqsHelperTests
    {
        private ITransaction _mockTransaction;

        [SetUp]
        public void SetUp()
        {
            _mockTransaction = Mock.Create<ITransaction>();

            Mock.Arrange(() => _mockTransaction.InsertDistributedTraceHeaders(Arg.IsAny<object>(), Arg.IsAny<Action<object, string, string>>()))
                .DoInstead((object carrier, Action<object, string, string> setter) =>
                {
                    setter(carrier, "traceparent", "traceparentvalue");
                    setter(carrier, "tracestate", "tracestatevalue");
                });
        }


        [Test]
        public void InsertDistributedTraceHeaders_ValidRequest_InsertsHeaders()
        {
            // Arrange
            var sendMessageRequest = new MockMessageRequest
            {
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    { "key1", new MessageAttributeValue { DataType = "String", StringValue = "value1" } },
                }
            };

            // Act
            SqsHelper.InsertDistributedTraceHeaders(_mockTransaction, sendMessageRequest, ["traceparent", "tracestate"]);

            // Assert
            Assert.That(sendMessageRequest.MessageAttributes, Has.Count.EqualTo(3));
            Assert.That(sendMessageRequest.MessageAttributes, Contains.Key("traceparent"));
            Assert.That(sendMessageRequest.MessageAttributes, Contains.Key("tracestate"));
            Assert.That(sendMessageRequest.MessageAttributes["traceparent"].StringValue, Is.EqualTo("traceparentvalue"));
            Assert.That(sendMessageRequest.MessageAttributes["tracestate"].StringValue, Is.EqualTo("tracestatevalue"));
        }
        [Test]
        [TestCase(7, true)]
        [TestCase(8, false)]
        public void InsertDistributedTraceHeaders_AttributeLimit_ExceedsLimitGracefully(int attributeCount, bool dtHeadersShouldBeAdded)
        {
            // Arrange
            var sendMessageRequest = new MockMessageRequest
            {
                MessageAttributes = new Dictionary<string, MessageAttributeValue>()
            };

            // Pre-populate the message attributes to reach the limit
            for (int i = 0; i < attributeCount; i++)
            {
                sendMessageRequest.MessageAttributes.Add($"key{i}", new MessageAttributeValue { DataType = "String", StringValue = $"value{i}" });
            }

            // Act
            SqsHelper.InsertDistributedTraceHeaders(_mockTransaction, sendMessageRequest, ["traceparent", "tracestate"]);

            // Assert
            if (dtHeadersShouldBeAdded)
            {
                Assert.That(sendMessageRequest.MessageAttributes, Has.Count.EqualTo(attributeCount + 2));
                Assert.That(sendMessageRequest.MessageAttributes, Does.ContainKey("traceparent"));
                Assert.That(sendMessageRequest.MessageAttributes, Does.ContainKey("tracestate"));
            }
            else
            {
                // assert that no additional headers were added
                Assert.That(sendMessageRequest.MessageAttributes, Has.Count.EqualTo(attributeCount));
                Assert.That(sendMessageRequest.MessageAttributes, Does.Not.ContainKey("traceparent"));
                Assert.That(sendMessageRequest.MessageAttributes, Does.Not.ContainKey("tracestate"));
            }
        }

        [Test]
        public void AcceptDistributedTraceHeaders_HeadersPresent_AppliesHeaders()
        {
            // Arrange
            var messageRequest = new MockMessageRequest
            {
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "traceparent", new MessageAttributeValue { DataType = "String", StringValue = "00-abcdef1234567890abcdef1234567890-abcdef123456-01" } },
                { "tracestate", new MessageAttributeValue { DataType = "String", StringValue = "congo=t61rcWkgMzE" } }
            }
            };

            var results = new Dictionary<string, string>();

            Mock.Arrange(() => _mockTransaction.AcceptDistributedTraceHeaders<IDictionary>(Arg.IsAny<IDictionary>(), Arg.IsAny<Func<IDictionary, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>()))
                .DoInstead((IDictionary carrier, Func<IDictionary, string, IEnumerable<string>> getter, TransportType _) =>
                {
                    var value = getter(carrier, "newrelic").SingleOrDefault();
                    if (!string.IsNullOrEmpty(value))
                        results["newrelic"] = value;

                    value = getter(carrier, "traceparent").SingleOrDefault();
                    if (!string.IsNullOrEmpty(value))
                        results["traceparent"] = value;

                    value = getter(carrier, "tracestate").SingleOrDefault();
                    if (!string.IsNullOrEmpty(value))
                        results["tracestate"] = value;
                });

            // Act
            SqsHelper.AcceptDistributedTraceHeaders(_mockTransaction, messageRequest.MessageAttributes);

            // Assert
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results, Contains.Key("traceparent").WithValue("00-abcdef1234567890abcdef1234567890-abcdef123456-01"));
            Assert.That(results, Contains.Key("tracestate").WithValue("congo=t61rcWkgMzE"));
            Assert.That(results, Does.Not.ContainKey("newrelic"));
        }
    }
}

namespace Amazon.SQS.Model
{
    public class MockMessageRequest
    {
        public Dictionary<string, MessageAttributeValue> MessageAttributes { get; set; }
    }

    public class MessageAttributeValue // name and namespace are required for reflection in SqsHelper.InsertDistributedTraceHeaders
    {
        public string DataType { get; set; }
        public string StringValue { get; set; }
    }
}
