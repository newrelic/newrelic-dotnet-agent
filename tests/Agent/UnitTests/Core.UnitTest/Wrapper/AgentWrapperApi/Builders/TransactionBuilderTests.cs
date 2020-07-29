/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class TransactionBuilderTests
    {
        private IConfiguration _configuration;
        private Transaction _builder;
        private TransactionFinalizedEvent _publishedEvent;

        private EventSubscription<TransactionFinalizedEvent> _eventSubscription;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _builder = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());
            _publishedEvent = null;
            _eventSubscription = new EventSubscription<TransactionFinalizedEvent>(e => _publishedEvent = e);
        }

        [TearDown]
        public void TearDown()
        {
            _eventSubscription?.Dispose();
            _eventSubscription = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void TransactionBuilderFinalizedEvent_IsPublished_IfNotEndedCleanly()
        {
            Assert.NotNull(_builder);

            _builder = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.NotNull(_publishedEvent);
        }

        [Test]
        public void TransactionBuilderFinalizedEvent_IsNotPublished_IfEndedCleanly()
        {
            Assert.NotNull(_builder);

            _builder.Finish();
            _builder = null;

            Assert.Null(_publishedEvent);
        }

        [Test]
        public void TransactionBuilderFinalizedEvent_IsNotPublishedASecondTime_IfBuilderGoesOutOfScopeAgain()
        {
            Assert.NotNull(_builder);

            _builder = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.NotNull(_publishedEvent);

            // The builder is now pinned to the event, but we can unpin it by unpinning the event
            _publishedEvent = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.Null(_publishedEvent);
        }

        [Test]
        [TestCase(1, 2, ExpectedResult = 1)]
        [TestCase(2, 2, ExpectedResult = 2)]
        [TestCase(3, 2, ExpectedResult = 2)]
        public int Add_Segment_When_Segment_Count_Considers_Configuration_TransactionTracerMaxSegments(int transactionTracerMaxSegmentThreashold, int segmentCount)
        {

            Mock.Arrange(() => _configuration.TransactionTracerMaxSegments).Returns(transactionTracerMaxSegmentThreashold);

            var transactionName = new WebTransactionName("WebTransaction", "Test");

            var transaction = new Transaction(_configuration, transactionName, Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator());

            for (int i = 0; i < segmentCount; i++)
            {
                new TypedSegment<ExternalSegmentData>(transaction, new MethodCallData("foo" + i, "bar" + i, 1), new ExternalSegmentData(new Uri("http://www.test.com"), "method")).End();
            }

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            return immutableTransaction.Segments.Count();

        }

    }
}
