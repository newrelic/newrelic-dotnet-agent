// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.TestUtilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Segments;

namespace CompositeTests
{
    public class DistributedTracingTests
    {
        private const string HeaderName = "newrelic";

        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void CreatedTransactionIdTraceIdSpanIdShouldBeLowerCase()
        {
            EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent());

            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: false);


            var segment = _agent.StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            foreach (var span in _compositeTestAgent.SpanEvents)
            {
                var intrinsicAttributes = span.IntrinsicAttributes();
                Assert.Multiple(() =>
                {
                    Assert.That(IsLowerCase(intrinsicAttributes["traceId"].ToString()), Is.True);
                    Assert.That(IsLowerCase(intrinsicAttributes["transactionId"].ToString()), Is.True);
                });
            }

            foreach (TransactionEventWireModel tx in _compositeTestAgent.TransactionEvents)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsLowerCase(tx.IntrinsicAttributes()["guid"].ToString()), Is.True);
                    Assert.That(IsLowerCase(tx.IntrinsicAttributes()["traceId"].ToString()), Is.True);
                });
            }
        }

        [Test]
        public void DatastoreSpanEvent_NullCommandTextDoesNotGenerateStatementAttribute()
        {
            var testHostName = "myHost";
            var testPort = "myPort";
            var testDBName = "myDatabase";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, null, null, testHostName, testPort, testDBName);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.That(spanEvents, Has.Count.EqualTo(2));

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            //Test that the information we get on our spans matches the info that we added to the request.
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "datastore");

            var agentAttributes = actualSpan.AgentAttributes();

            Assert.Multiple(() =>
            {
                //The specific test
                Assert.That(agentAttributes.ContainsKey("db.statement"), Is.False);
                Assert.That(agentAttributes["db.instance"], Is.EqualTo(testDBName));
                Assert.That(agentAttributes["peer.hostname"], Is.EqualTo(testHostName));
                Assert.That(agentAttributes["peer.address"], Is.EqualTo($"{testHostName}:{testPort}"));
            });
        }

        [Test]
        public void DatastoreSpanEvent_NotNullCommandTextGeneratesStatementAttribute()
        {
            var testHostName = "myHost";
            var testPort = "myPort";
            var testDBName = "myDatabase";
            var testCommand = "myStatement";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null, testCommand, null, testHostName, testPort, testDBName);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.That(spanEvents, Has.Count.EqualTo(2));

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // Test the spans match the payload info from which they were created. 
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "datastore");

            var agentAttributes = actualSpan.AgentAttributes();

            Assert.Multiple(() =>
            {
                //The specific test
                Assert.That(agentAttributes["db.statement"], Is.EqualTo(testCommand));
                Assert.That(agentAttributes["db.instance"], Is.EqualTo(testDBName));
                Assert.That(agentAttributes["peer.hostname"], Is.EqualTo(testHostName));
                Assert.That(agentAttributes["peer.address"], Is.EqualTo($"{testHostName}:{testPort}"));
            });
        }

        [Test]
        public void MessageBrokerSegmentResultsInSpanEventCategoryOfGeneric()
        {
            var vendorName = "RabbitMQ";
            var routingKey = "queueName"; //TOPIC has a . QUEUE no .

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                brokerVendorName: vendorName,
                destination: routingKey);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartMessageBrokerSegmentOrThrow(vendorName, MessageBrokerDestinationType.Queue,
                routingKey, MessageBrokerAction.Consume);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.That(spanEvents, Has.Count.EqualTo(2));

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            Assert.That(rootSpan, Is.Not.Null);

            var rootSpanIntrinsicAttributes = rootSpan.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                Assert.That((string)rootSpanIntrinsicAttributes["traceId"], Is.EqualTo(Payload.TraceId));
                Assert.That((double)rootSpanIntrinsicAttributes["priority"], Is.EqualTo(Payload.Priority));
                Assert.That((bool)rootSpanIntrinsicAttributes["sampled"], Is.EqualTo(Payload.Sampled));
                Assert.That((string)rootSpanIntrinsicAttributes["parentId"], Is.EqualTo(Payload.Guid));
                Assert.That((string)rootSpanIntrinsicAttributes["category"], Is.EqualTo("generic"));
                Assert.That((bool)rootSpanIntrinsicAttributes["nr.entryPoint"], Is.True);
            });

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            Assert.That(actualSpan, Is.Not.Null);

            var actualSpanIntrinsicAttributes = actualSpan.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                Assert.That((string)actualSpanIntrinsicAttributes["traceId"], Is.EqualTo(Payload.TraceId));
                Assert.That((double)actualSpanIntrinsicAttributes["priority"], Is.EqualTo(Payload.Priority));
                Assert.That((bool)actualSpanIntrinsicAttributes["sampled"], Is.EqualTo(Payload.Sampled));
                Assert.That((string)actualSpanIntrinsicAttributes["parentId"], Is.EqualTo((string)rootSpanIntrinsicAttributes["guid"]));
                Assert.That((string)actualSpanIntrinsicAttributes["transactionId"], Is.EqualTo((string)rootSpanIntrinsicAttributes["transactionId"]));
                Assert.That((string)actualSpanIntrinsicAttributes["category"], Is.EqualTo("generic"));
            });
        }

        [Test]
        public void ExternalHttpSpanEvent_HasExpectedAttributes()
        {
            var url = "http://127.0.0.2:123/Fake/Url";
            var method = "POST";

            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(NewRelicHeaders, HeaderFunctions.GetHeaders, TransportType.HTTP);

            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri(url), method);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents;

            Assert.That(spanEvents, Has.Count.EqualTo(2));

            // The faux span we create to contain the actual spans.
            var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            // The span created from the segment at the top of the test.
            var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes().ContainsKey("nr.entryPoint"));

            //Test that the information we get on our spans matches the info that we added to the request.
            TestPayloadInfoMatchesSpanInfo(Payload, rootSpan, actualSpan, "http");

            var agentAttributes = actualSpan.AgentAttributes();
            var intrinsicAttributes = actualSpan.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                //The specific test
                Assert.That(agentAttributes["http.url"], Is.EqualTo(url));
                Assert.That(agentAttributes["http.request.method"], Is.EqualTo(method));
                Assert.That(intrinsicAttributes["server.address"], Is.EqualTo("127.0.0.2"));
                Assert.That(intrinsicAttributes["server.port"], Is.EqualTo(123));
            });
        }

        private static Dictionary<string, string> NewRelicHeaders
        {
            get
            {
                var headers = new Dictionary<string, string>();

                var encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(Payload);
                headers.Add(HeaderName, encodedPayload);

                return headers;
            }
        }

        private static DistributedTracePayload Payload => new DistributedTracePayload
        {
            Type = "App",
            AccountId = "33",
            AppId = "2827902",
            TraceId = "d6b4ba0c3a712ca",
            Priority = 1.1f,
            Sampled = true,
            Timestamp = DateTime.UtcNow,
            Guid = "7d3efb1b173fecfa",
            TrustKey = "33",
            TransactionId = "e8b91a159289ff74"
        };

        private static void TestPayloadInfoMatchesSpanInfo(DistributedTracePayload payload, ISpanEventWireModel rootSpan, ISpanEventWireModel actualSpan, string actualCategory)
        {
            Assert.That(rootSpan, Is.Not.Null);

            var rootSpanIntrinsicAttributes = rootSpan.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                Assert.That(AttributeComparer.IsEqualTo(payload.TraceId, rootSpanIntrinsicAttributes["traceId"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(payload.Priority, rootSpanIntrinsicAttributes["priority"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(payload.Sampled, rootSpanIntrinsicAttributes["sampled"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(payload.Guid, rootSpanIntrinsicAttributes["parentId"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo("generic", rootSpanIntrinsicAttributes["category"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(true, rootSpanIntrinsicAttributes["nr.entryPoint"]), Is.True);

                Assert.That(actualSpan, Is.Not.Null);
            });

            var actualSpanIntrinsicAttributes = actualSpan.IntrinsicAttributes();

            Assert.Multiple(() =>
            {
                Assert.That(AttributeComparer.IsEqualTo(payload.TraceId, actualSpanIntrinsicAttributes["traceId"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(payload.Priority, actualSpanIntrinsicAttributes["priority"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(payload.Sampled, actualSpanIntrinsicAttributes["sampled"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(rootSpanIntrinsicAttributes["guid"], actualSpanIntrinsicAttributes["parentId"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(rootSpanIntrinsicAttributes["transactionId"], actualSpanIntrinsicAttributes["transactionId"]), Is.True);
                Assert.That(AttributeComparer.IsEqualTo(actualCategory, actualSpanIntrinsicAttributes["category"]), Is.True);
            });
        }

        private bool IsLowerCase(string id)
        {
            return id.Equals(id.ToLowerInvariant());
        }
    }
}
