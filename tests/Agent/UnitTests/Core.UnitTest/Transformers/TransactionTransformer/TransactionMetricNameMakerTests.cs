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

        [TearDown]
        public void TearDown()
        {
            _metricNameService.Dispose();
        }

        [Test]
        public void BuiltTransactionName_BuildsWebTransactionMetricName_IfWebTransactionName()
        {
            var transactionName = TransactionName.ForWebTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("WebTransaction/foo/bar"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsUriWebTransactionMetricName_IfUriTransactionName()
        {
            Mock.Arrange(() => _metricNameService.NormalizeUrl(Arg.IsAny<string>()))
                .Returns<string>((uri) => uri + "/normalized");

            var transactionName = TransactionName.ForUriTransaction("http://www.google.com/yomama/normalized");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("WebTransaction/Uri/http://www.google.com/yomama/normalized"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsOtherTransactionMetricName_IfOtherTransactionName()
        {
            var transactionName = TransactionName.ForOtherTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/foo/bar"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomWebTransactionName()
        {
            var transactionName = TransactionName.ForCustomTransaction(true, "foo", 255);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("WebTransaction/Custom/foo"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsCustomTransactionMetricName_IfCustomNonWebTransactionName()
        {
            var transactionName = TransactionName.ForCustomTransaction(false, "foo", 255);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/Custom/foo"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithQueueName_IfNamedMessageBrokerTransactionName()
        {
            var transactionName = TransactionName.ForBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "baz");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/Message/bar/Queue/Named/baz"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsMessageBrokerTransactionMetricNameWithoutQueueName_IfUnnamedMessageBrokerTransactionName()
        {
            var transactionName = TransactionName.ForBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", null);

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/Message/bar/Queue/Temp"));
            });
        }

        [Test]
        public void BuiltTransactionName_RunsThroughMetricNameService()
        {
            Mock.Arrange(() => _metricNameService.RenameTransaction(Arg.IsAny<TransactionMetricName>()))
                .Returns(_ => new TransactionMetricName("WebTransaction", "NewName"));

            var transactionName = TransactionName.ForWebTransaction("foo", "bar");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("WebTransaction/NewName"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsKafkaMessageBrokerTransactionMetricNameWithQueueName()
        {
            var transactionName = TransactionName.ForKafkaBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "baz");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/Message/bar/Queue/Consume/Named/baz"));
            });
        }

        [Test]
        public void BuiltTransactionName_BuildsKafkaMessageBrokerTransactionMetricNameWithTemp_IfEmptyDestinationSpecified()
        {
            var transactionName = TransactionName.ForKafkaBrokerTransaction(Extensions.Providers.Wrapper.MessageBrokerDestinationType.Queue, "bar", "");

            var builtName = _transactionMetricNameMaker.GetTransactionMetricName(transactionName);

            Assert.Multiple(() =>
            {
                Assert.That(builtName.ShouldIgnore, Is.False);
                Assert.That(builtName.PrefixedName, Is.EqualTo("OtherTransaction/Message/bar/Queue/Consume/Named/Temp"));
            });
        }
    }
}
