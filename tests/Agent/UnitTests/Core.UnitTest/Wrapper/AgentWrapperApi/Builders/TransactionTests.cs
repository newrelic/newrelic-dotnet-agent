// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.DistributedTracing;
using System.Collections.Generic;
using NewRelic.Agent.TestUtilities;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class TransactionTests
    {
        private IConfiguration _configuration;
        private Transaction _transaction;
        private IDatabaseService _databaseService;
        private IErrorService _errorService;
        private IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        private TransactionFinalizedEvent _publishedEvent;
        private EventSubscription<TransactionFinalizedEvent> _eventSubscription;
        private AttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        private const float Priority = 0.5f;
        private object _wrapperToken;

        [SetUp]
        public void SetUp()
        {
            _configuration = Mock.Create<IConfiguration>();
            var configurationService = Mock.Create<IConfigurationService>();
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _databaseService = new DatabaseService();
            _errorService = new ErrorService(configurationService);
            _distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _transaction = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, Priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);
            _publishedEvent = null;
            _eventSubscription = new EventSubscription<TransactionFinalizedEvent>(e => _publishedEvent = e);
            _wrapperToken = new object();
        }

        [TearDown]
        public void TearDown()
        {
            _transaction = null;

            _eventSubscription?.Dispose();
            _eventSubscription = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void TransactionDoesNotImplementIDisposable()
        {
            // If our Transaction class is IDisposable, 3rd party libraries can potentially dispose of it thereby leading to unexpected behavior.
            // See: https://source.datanerd.us/dotNetAgent/dotnet_agent/pull/2323
            Assert.IsNotInstanceOf<IDisposable>(_transaction);
        }

        [Test]
        public void TransactionFinalizedEvent_IsPublished_IfNotEndedCleanly()
        {
            Assert.NotNull(_transaction);

            _transaction = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.NotNull(_publishedEvent);
        }

        [Test]
        public void TransactionFinalizedEvent_IsNotPublished_IfEndedCleanly()
        {
            Assert.NotNull(_transaction);

            _transaction.Finish();
            _transaction = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.Null(_publishedEvent);
        }

        [Test]
        public void TransactionShouldOnlyFinishOnce()
        {
            Assert.NotNull(_transaction);
            Assert.False(_transaction.IsFinished, "The transaction should not be finished yet.");

            var finishedTransaction = _transaction.Finish();

            Assert.True(finishedTransaction, "Transaction was not finished when it should have been finished.");
            Assert.True(_transaction.IsFinished, "transaction.IsFinished should be true.");
            Assert.Null(_publishedEvent, "The TransactionFinalizedEvent should not be triggered after the first call to finish.");

            finishedTransaction = _transaction.Finish();
            var isFinished = _transaction.IsFinished;
            _transaction = null;

            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            Assert.False(finishedTransaction, "Transaction was finished again when it should only be finished once.");
            Assert.True(isFinished, "transaction.IsFinished should still be true.");
            Assert.Null(_publishedEvent, "The TransactionFinalizedEvent should not be triggered when the transaction is already finished.");
        }

        [Test]
        public void TransactionFinalizedEvent_IsNotPublishedASecondTime_IfBuilderGoesOutOfScopeAgain()
        {
            Assert.NotNull(_transaction);

            _transaction = null;

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

            var transactionName = TransactionName.ForWebTransaction("WebTransaction", "Test");


            var transaction = new Transaction(_configuration, transactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), _databaseService, Priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);

            for (int i = 0; i < segmentCount; i++)
            {
                var segment = new Segment(transaction, new MethodCallData("foo" + i, "bar" + i, 1));
                segment.SetSegmentData(new ExternalSegmentData(new Uri("http://www.test.com"), "method"));

                segment.End();
            }

            var immutableTransaction = transaction.ConvertToImmutableTransaction();
            return immutableTransaction.Segments.Count();

        }

        [Test]
        public void TransactionTraceIdShouldBe32Char()
        {
            // Arrange
            Mock.Arrange(() => _configuration.DistributedTracingEnabled).Returns(true);

            var tx = new Transaction(
                _configuration,
                Arg.IsAny<TransactionName>(),
                Arg.IsAny<ISimpleTimer>(),
                Arg.IsAny<DateTime>(),
                Arg.IsAny<ICallStackManager>(),
                Arg.IsAny<IDatabaseService>(),
                Arg.IsAny<float>(),
                Arg.IsAny<IDatabaseStatementParser>(),
                Arg.IsAny<IDistributedTracePayloadHandler>(),
                Arg.IsAny<IErrorService>(),
                Arg.IsAny<IAttributeDefinitions>()
            );

            // Assert
            Assert.AreEqual(32, tx.TraceId.Length);
        }

        /// <summary>
        /// https://source.datanerd.us/agents/agent-specs/blob/2ad6637ded7ec3784de40fbc88990e06525127b8/Cross-Application-Tracing-PORTED.md#guid
        /// </summary>
        [Test]
        public void TransactionGuidShouldBe16CharacterHex()
        {
            // Arrange
            var name = TransactionName.ForWebTransaction("foo", "bar");
            var startTime = DateTime.Now;
            var timer = Mock.Create<ISimpleTimer>();
            var callStackManager = Mock.Create<ICallStackManager>();
            var sqlObfuscator = Mock.Create<IDatabaseService>();
            var tx = new Transaction(_configuration, name, timer, startTime, callStackManager, sqlObfuscator, Priority, Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);

            // Assert
            Assert.That(tx.Guid, Is.Not.Null);

            const string guidFormatPattern = @"^[0-9A-Fa-f]{16}$";
            Assert.That(tx.Guid, Does.Match(guidFormatPattern));
        }

        [Test]
        public void TransactionShouldOnlyCaptureResponseTimeOnce()
        {
            //Verify initial state
            Assert.Null(_transaction.ResponseTime, "ResponseTime should initially be null.");

            //First attempt to capture the response time
            Assert.True(_transaction.TryCaptureResponseTime(), "ResponseTime should have been captured but was not captured.");

            //Verify that the response time was captured
            var capturedResponseTime = _transaction.ResponseTime;
            Assert.NotNull(capturedResponseTime, "ResponseTime should have a value.");

            //Second attempt to capture the response time
            Assert.False(_transaction.TryCaptureResponseTime(), "ResponseTime should not be captured again, but it was.");
            Assert.AreEqual(capturedResponseTime, _transaction.ResponseTime, "ResponseTime should still have the same value as the originally captured ResponseTime.");
        }

        [Test]
        public void TransactionGetWrapperTokenEqualsPassedInToken()
        {
            _transaction.SetWrapperToken(_wrapperToken);
            Assert.AreEqual(_wrapperToken, _transaction.GetWrapperToken());
        }

        [Test]
        public void AddRequestParameter_LastInWins()
        {

            var key = "testKey";
            var outputKey = "request.parameters." + key;
            var valueA = "valueA";
            var valueB = "valueB";

            _transaction.SetRequestParameters(new[]
            {
                new KeyValuePair<string,string>(key, valueA),
                new KeyValuePair<string,string>(key, valueB)
            });

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();

            var requestParameters = immutableTransactionMetadata.UserAndRequestAttributes.ToDictionary();

            var result = requestParameters[outputKey];

            Assert.AreEqual(result, valueB);
        }

        [Test]
        public void AddUserAttribute_LastInWins()
        {
            var key = "testKey";
            var outputKey = "request.parameters." + key;
            var valueA = "valueA";
            var valueB = "valueB";

            _transaction.SetRequestParameters(new[]
           {
                new KeyValuePair<string,string>(key, valueA),
                new KeyValuePair<string,string>(key, valueB)
            });

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();

            var userAttributes = immutableTransactionMetadata.UserAndRequestAttributes.ToDictionary();

            var result = userAttributes[outputKey];

            Assert.AreEqual(result, valueB);
        }

        #region User Tracking Tests
        [Test]
        public void UserTracking_NoEndUserIdAttributeIfNotSet()
        {
            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();

            var userAttributes = immutableTransactionMetadata.UserAndRequestAttributes.ToDictionary();
            var hasUserIdAttribute = userAttributes.TryGetValue(_attribDefs.EndUserId.Name, out var userIdValue);
            Assert.False(hasUserIdAttribute);
        }

        [Test]
        [TestCase("CustomUserId", true)]
        [TestCase("", false)]
        [TestCase(" ", false)]
        [TestCase(null, false)]

        public void UserTracking_ShouldAddEndUserIdAttributeWhenNotNullOrWhitespace(string expectedUserId, bool endUserAttributeShouldExist)
        {
            _transaction.SetUserId(expectedUserId);

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            var userAttributes = immutableTransactionMetadata.UserAndRequestAttributes.ToDictionary();
            var hasUserIdAttribute = userAttributes.TryGetValue(_attribDefs.EndUserId.Name, out var userIdValue);

            Assert.AreEqual(endUserAttributeShouldExist, hasUserIdAttribute);
            if (endUserAttributeShouldExist)
            {
                Assert.AreEqual(expectedUserId, userIdValue);
            }
        }

        [Test]
        public void UserTracking_SetUserIdMultipleTimesLastOneWins()
        {
            var expectedUserId1 = "CustomUserId";
            var expectedUserId2 = "AnotherCustomUserId";
            _transaction.SetUserId(expectedUserId1);
            _transaction.SetUserId(expectedUserId2);

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            var userAttributes = immutableTransactionMetadata.UserAndRequestAttributes.ToDictionary();
            var hasUserIdAttribute = userAttributes.TryGetValue(_attribDefs.EndUserId.Name, out var userIdValue);
            Assert.True(hasUserIdAttribute);
            Assert.AreEqual(expectedUserId2, userIdValue);
        }
        #endregion

    }
}
