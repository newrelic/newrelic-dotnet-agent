using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
	[TestFixture]
	public class TransactionEventMakerTests
	{
		[NotNull] private TransactionEventMaker _transactionEventMaker;

		[SetUp]
		public void SetUp()
		{
			var attributeService = Mock.Create<IAttributeService>();
			Mock.Arrange(() => attributeService.FilterAttributes(Arg.IsAny<Attributes>(), Arg.IsAny<AttributeDestinations>())).Returns<Attributes, AttributeDestinations>((attrs, _) => attrs);
			_transactionEventMaker = new TransactionEventMaker(attributeService);
		}

		[Test]
		public void GetTransactionEvent_ReturnsSyntheticEvent()
		{
			// ARRANGE
			var immutableTransaction = BuildTestTransaction(true);
			var attributes = Mock.Create<Attributes>();

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsTrue(transactionEvent.IsSynthetics());
		}

		[Test]
		public void GetTransactionEvent_ReturnsCorrectAttributes()
		{
			// ARRANGE
			var immutableTransaction = BuildTestTransaction(isSynthetics: false);
			var attributes = new Attributes();
			attributes.Add(Attribute.BuildTypeAttribute(TypeAttributeValue.Transaction));
			attributes.Add(Attribute.BuildResponseStatusAttribute("status"));
			attributes.Add(Attribute.BuildRequestUriAttribute("http://foo.com"));
			attributes.Add(Attribute.BuildCustomAttribute("foo", "bar"));
			attributes.Add(Attribute.BuildCustomErrorAttribute("fiz", "baz"));

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(1, transactionEvent.IntrinsicAttributes.Count),
				() => Assert.AreEqual("Transaction", transactionEvent.IntrinsicAttributes["type"]),
				() => Assert.AreEqual(2, transactionEvent.AgentAttributes.Count),
				() => Assert.AreEqual("status", transactionEvent.AgentAttributes["response.status"]),
				() => Assert.AreEqual("http://foo.com", transactionEvent.AgentAttributes["request.uri"]),
				() => Assert.AreEqual(2, transactionEvent.UserAttributes.Count),
				() => Assert.AreEqual("bar", transactionEvent.UserAttributes["foo"]),
				() => Assert.AreEqual("baz", transactionEvent.UserAttributes["fiz"])
			);
		}

		[Test]
		public void GetTransactionEvent_DoesNotReturnsSyntheticEvent()
		{
			// ARRANGE
			var immutableTransaction = BuildTestTransaction(false);
			var attributes = Mock.Create<Attributes>();

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsFalse(transactionEvent.IsSynthetics());
		}

		[Test]
		public void GetTransactionEvent_ReturnsCorrectDistributedTraceAttributes()
		{
			var account = "273070";
			var app = "217958";
			var transportType = "http";
			var transportDuration = new TimeSpan(0,0,5);
			var guid = "squid";
			var parentId = "parentid";
			var priority = .3f;
			var sampled = true;
			var traceId = "traceid";
			var parentType = "Mobile";

		// ARRANGE
		var immutableTransaction = BuildTestTransaction(isSynthetics: false);
			var attributes = new Attributes();
			attributes.Add(Attribute.BuildParentTypeAttribute(parentType));
			attributes.Add(Attribute.BuildParentAppAttribute(app));
			attributes.Add(Attribute.BuildParentAccountAttribute(account));
			attributes.Add(Attribute.BuildParentTransportTypeAttribute(transportType));
			attributes.Add(Attribute.BuildParentTransportDurationAttribute(transportDuration));
			attributes.Add(Attribute.BuildParentIdAttribute(parentId));
			attributes.Add(Attribute.BuildGuidAttribute(guid));
			attributes.Add(Attribute.BuildDistributedTraceIdAttributes(traceId));
			attributes.Add(Attribute.BuildPriorityAttribute(priority));
			attributes.Add(Attribute.BuildSampledAttribute(sampled));

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			NrAssert.Multiple(
				() => Assert.AreEqual(10, transactionEvent.IntrinsicAttributes.Count),
				() => Assert.AreEqual(parentType, transactionEvent.IntrinsicAttributes["parent.type"]),
				() => Assert.AreEqual(app, transactionEvent.IntrinsicAttributes["parent.app"]),
				() => Assert.AreEqual(account, transactionEvent.IntrinsicAttributes["parent.account"]),
				() => Assert.AreEqual(transportType, transactionEvent.IntrinsicAttributes["parent.transportType"]),
				() => Assert.AreEqual(transportDuration.TotalSeconds, transactionEvent.IntrinsicAttributes["parent.transportDuration"]),
				() => Assert.AreEqual(parentId, transactionEvent.IntrinsicAttributes["parentId"]),
				() => Assert.AreEqual(guid, transactionEvent.IntrinsicAttributes["guid"]),
				() => Assert.AreEqual(traceId, transactionEvent.IntrinsicAttributes["traceId"]),
				() => Assert.AreEqual(priority, transactionEvent.IntrinsicAttributes["priority"]),
				() => Assert.AreEqual(sampled, transactionEvent.IntrinsicAttributes["sampled"])
			);
		}

		private static ImmutableTransaction BuildTestTransaction(bool isSynthetics)
		{
			var name = new WebTransactionName("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var userErrorAttributes = new ConcurrentDictionary<string, object>();
			userErrorAttributes.TryAdd("CustomErrorAttrKey", "CustomErrorAttrValue");

			var priority = 0.5f;
			var metadata = new ImmutableTransactionMetadata(
				"uri",
				"originalUri",
				"referrerUri",
				new TimeSpan(1),
				new ConcurrentDictionary<string, string>(),
				new ConcurrentDictionary<string, object>(),
				userErrorAttributes,
				200,
				201,
				new List<ErrorData>(),
				new List<ErrorData>(),
				"crossApplicationReferrerPathHash",
				"crossApplicationPathHash",
				new List<string>(),
				"crossApplicationReferrerTransactionGuid",
				"crossApplicationReferrerProcessId",
				"crossApplicationReferrerTripId",
				"distributedTraceType",
				"distributedTraceApp",
				"distributedTraceAccount",
				"distributedTraceTransportType",
				"distributedTraceGuid",
				TimeSpan.MinValue, // DistributedTraceTransportDuration
				"distributedTraceTraceId",
				"distributedTransactionId",
				"distributedTraceTrustKey",
				false,  // DistributedTraceSampled,
				false,  // HasOutgoingDistributedTracePayload
				false,  // HasIncomingDistributedTracePayload
				"syntheticsResourceId",
				"syntheticsJobId",
				"syntheticsMonitorId",
				isSynthetics,
				false,
				priority);

			var guid = Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), guid, true, true, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}
	}
}