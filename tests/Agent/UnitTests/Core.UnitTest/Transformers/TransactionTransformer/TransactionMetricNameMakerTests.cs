// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class TransactionMetricNameMakerTests
    {
        private TransactionMetricNameMaker _transactionMetricNameMaker;

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
            var transactionName = TransactionName.ForWebTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("WebTransaction/foo/bar", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsUriWebTransactionMetricName_IfUriTransactionName()
        {
            Mock.Arrange(() => _metricNameService.NormalizeUrl(Arg.IsAny<string>()))
                .Returns<string>((uri) => uri + "/normalized");

            var transactionName = TransactionName.ForUriTransaction("http://www.google.com/yomama/normalized");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("WebTransaction/Uri/http://www.google.com/yomama/normalized", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsOtherTransactionMetricName_IfOtherTransactionName()
        {
            var transactionName = TransactionName.ForOtherTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/foo/bar", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomWebTransactionName()
        {
            var transactionName = TransactionName.ForCustomTransaction(true, "foo", 255);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("WebTransaction/Custom/foo", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomNonWebTransactionName()
        {
            var transactionName = TransactionName.ForCustomTransaction(false, "foo", 255);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/Custom/foo", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithQueueName_IfNamedMessageBrokerTransactionName()
        {
            var transactionName = TransactionName.ForBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "baz");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/Message/bar/Queue/Named/baz", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithoutQueueName_IfUnnamedMessageBrokerTransactionName()
        {
            var transactionName = TransactionName.ForBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", null);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/Message/bar/Queue/Temp", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_RunsThroughMetricNameService()
        {
            Mock.Arrange(() => _metricNameService.RenameTransaction(Arg.IsAny<TransactionMetricName>()))
                .Returns(_ => new TransactionMetricName("WebTransaction", "NewName"));

            var transactionName = TransactionName.ForWebTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("WebTransaction/NewName", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsKafkaMessageBrokerTransactionMetricNameWithQueueName()
        {
            var transactionName = TransactionName.ForKafkaBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "baz");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/Message/bar/Queue/Consume/Named/baz", builtName.PrefixedName);
        }

        [Test]
        public void BuiltTransactionName_BuildsKafkaMessageBrokerTransactionMetricNameWithTemp_IfEmptyDestinationSpecified()
        {
            var transactionName = TransactionName.ForKafkaBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.IsFalse(builtName.ShouldIgnore);
            Assert.AreEqual("OtherTransaction/Message/bar/Queue/Consume/Named/Temp", builtName.PrefixedName);
        }
    }
}
