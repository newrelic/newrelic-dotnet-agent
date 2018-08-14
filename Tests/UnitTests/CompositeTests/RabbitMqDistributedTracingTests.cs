using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
	public class RabbitMqDistributedTracingTests
	{
		private const string VendorName = "RabbitMQ";
		private const string RoutingKey = "queueName"; //TOPIC has a . QUEUE no .
		private const string TransportType = "AMQP";
		private const string HeaderName = "newrelic";

		private static CompositeTestAgent _compositeTestAgent;

		private IAgentWrapperApi _agentWrapperApi;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void ProcessInboundRequest_AcceptsPayload_ConfirmAttributes()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();
			
			using (_agentWrapperApi.CreateMessageBrokerTransaction(MessageBrokerDestinationType.Queue, VendorName, RoutingKey))
			{
				_agentWrapperApi.ProcessInboundRequest(NewRelicHeaders, TransportType);
				var segment = _agentWrapperApi.StartMessageBrokerSegmentOrThrow(VendorName, MessageBrokerDestinationType.Queue,
					RoutingKey, MessageBrokerAction.Consume);
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var spanEvents = _compositeTestAgent.SpanEvents;

			Assert.AreEqual(2, spanEvents.Count);

			// The faux span we create to contain the actual spans.
			var rootSpan = spanEvents.FirstOrDefault(span => span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			Assert.NotNull(rootSpan);
			Assert.AreEqual(Payload.TraceId, (string)rootSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(Payload.Priority, (float)rootSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(Payload.Sampled, (bool)rootSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual(Payload.Guid, (string)rootSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual("generic", (string)rootSpan.IntrinsicAttributes["category"]);
			Assert.True((bool)rootSpan.IntrinsicAttributes["nr.entryPoint"]);

			// The span created from the segment at the top of the test.
			var actualSpan = spanEvents.FirstOrDefault(span => !span.IntrinsicAttributes.ContainsKey("nr.entryPoint"));

			Assert.NotNull(actualSpan);
			Assert.AreEqual(Payload.TraceId, (string)actualSpan.IntrinsicAttributes["traceId"]);
			Assert.AreEqual(Payload.Priority, (float)actualSpan.IntrinsicAttributes["priority"]);
			Assert.AreEqual(Payload.Sampled, (bool)actualSpan.IntrinsicAttributes["sampled"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["guid"], (string)actualSpan.IntrinsicAttributes["parentId"]);
			Assert.AreEqual((string)rootSpan.IntrinsicAttributes["transactionId"], (string)actualSpan.IntrinsicAttributes["transactionId"]);
			Assert.AreEqual("generic", (string)actualSpan.IntrinsicAttributes["category"]);
		}

		[Test]
		public void StartRabbitMQSegmentAndCreateDistributedTracePayload_CreatesPayloadAndUpdatesHeaders()
		{
			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.AccountId = "33";
			_compositeTestAgent.ServerConfiguration.PrimaryApplicationId = "2827902";
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();

			// If test is successful, the payload will be added to this object
			var headers = new Dictionary<string, object>();

			using (_agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartRabbitMqPayloadCreationSegmentOrThrow(VendorName, MessageBrokerDestinationType.Queue,
					RoutingKey, MessageBrokerAction.Consume, null, headers);
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var spanEvents = _compositeTestAgent.SpanEvents;

			Assert.AreEqual(2, spanEvents.Count);
			Assert.NotNull(headers.Keys.FirstOrDefault(key => key.ToLowerInvariant() == HeaderName));
			Assert.NotNull(headers.FirstOrDefault(pair => pair.Key.ToLowerInvariant() == HeaderName).Value);
		}

		private static Dictionary<string, string> NewRelicHeaders
		{
			get
			{
				var headers = new Dictionary<string, string>();

				var encodedPayload = HeaderEncoder.SerializeAndEncodeDistributedTracePayload(Payload);
				headers.Add(HeaderName, encodedPayload);

				return headers;
			}
		}

		private static DistributedTracePayload Payload => new DistributedTracePayload
		{
			Type = "App",
			AccountId = "33",
			AppId = "2827902",
			TraceId = "d6b4ba0c3a712ca".ToUpperInvariant(),
			Priority = 1.1f,
			Sampled = true,
			Timestamp = DateTime.UtcNow,
			Guid = "7d3efb1b173fecfa".ToUpperInvariant(),
			TrustKey = "33",
			TransactionId = "e8b91a159289ff74".ToUpperInvariant()
		};
	}
}
