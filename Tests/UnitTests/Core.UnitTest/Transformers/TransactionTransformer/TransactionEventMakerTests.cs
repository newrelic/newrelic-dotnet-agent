using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Collections;
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
			var immutableTransaction = BuildTestTransaction(true);
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
			var immutableTransaction = BuildTestTransaction();
			var attributes = Mock.Create<Attributes>();

			// ACT
			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes);

			// ASSERT
			Assert.NotNull(transactionEvent);
			Assert.IsFalse(transactionEvent.IsSynthetics());
		}

		private static ImmutableTransaction BuildTestTransaction(Boolean isSynthetics = false, Boolean hasCatResponseHeaders = false)
		{
			var name = new WebTransactionName("foo", "bar");
			var segments = Enumerable.Empty<Segment>();
			var userErrorAttributes = new ConcurrentDictionary<String, Object>();
			userErrorAttributes.Add("CustomErrorAttrKey", "CustomErrorAttrValue");

			float priority = 0.5f;
			var metadata = new ImmutableTransactionMetadata("uri", "originalUri", "referrerUri",
			new TimeSpan(1), new ConcurrentDictionary<String, String>(),
			new ConcurrentDictionary<String, Object>(),
			userErrorAttributes, 200,
			201, new List<ErrorData>(),
			new List<ErrorData>(), "crossApplicationReferrerPathHash",
			"crossApplicationPathHash",
			new List<String>(), "crossApplicationReferrerTransactionGuid",
			"crossApplicationReferrerProcessId", "crossApplicationReferrerTripId", "syntheticsResourceId",
			"syntheticsJobId", "syntheticsMonitorId", isSynthetics, hasCatResponseHeaders, priority);

			var guid = Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, metadata, DateTime.UtcNow, TimeSpan.FromSeconds(1), guid, true, true, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}
	}
}