using System;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
	[TestFixture]
	public class TransactionMetricNameMakerTests
	{
		[NotNull]
		private TransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private IMetricNameService _metricNameService;

		[SetUp]
		public void SetUp()
		{
			_metricNameService = Mock.Create<IMetricNameService>();
			Mock.Arrange(() => _metricNameService.RenameTransaction(Arg.IsAny<TransactionMetricName>()))
				.Returns(name => name);

			_transactionMetricNameMaker = new TransactionMetricNameMaker(_metricNameService);
		}

		[Test]
		public void BuiltTransactionName_BuildsWebTransactionMetricName_IfWebTransactionName()
		{
			var transactionName = new WebTransactionName("foo", "bar");

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("WebTransaction/foo/bar", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsUriWebTransactionMetricName_IfUriTransactionName()
		{
			Mock.Arrange(() => _metricNameService.NormalizeUrl(Arg.IsAny<String>()))
				.Returns<String>((uri) => uri + "/normalized");
			var transactionName = new UriTransactionName("http://www.google.com/yomama");

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("WebTransaction/Uri/http://www.google.com/yomama/normalized", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsOtherTransactionMetricName_IfOtherTransactionName()
		{
			var transactionName = new OtherTransactionName("foo", "bar");

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("OtherTransaction/foo/bar", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomWebTransactionName()
		{
			var transactionName = new CustomTransactionName("foo", true);

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("WebTransaction/Custom/foo", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomNonWebTransactionName()
		{
			var transactionName = new CustomTransactionName("foo", false);

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("OtherTransaction/Custom/foo", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithQueueName_IfNamedMessageBrokerTransactionName()
		{
			var transactionName = new MessageBrokerTransactionName("foo", "bar", "baz");

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("OtherTransaction/Message/bar/foo/Named/baz", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithoutQueueName_IfUnnamedMessageBrokerTransactionName()
		{
			var transactionName = new MessageBrokerTransactionName("foo", "bar", null);

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("OtherTransaction/Message/bar/foo/Temp", builtName.PrefixedName);
		}

		[Test]
		public void BuiltTransactionName_RunsThroughMetricNameService()
		{
			Mock.Arrange(() => _metricNameService.RenameTransaction(Arg.IsAny<TransactionMetricName>()))
				.Returns(_ => new TransactionMetricName("WebTransaction", "NewName"));

			var transactionName = new WebTransactionName("foo", "bar");

			var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

			Assert.IsFalse(builtName.ShouldIgnore);
			Assert.AreEqual("WebTransaction/NewName", builtName.PrefixedName);
		}
	}
}
