using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;


namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
    [TestFixture]
    public class AgentWrapperApiTests
    {
        private ITransaction _transaction;
        private ITransactionService _transactionService;
        private IAgentWrapperApi _agentWrapperApi;
        private ITransactionTransformer _transactionTransformer;
        private ICallStackManager _callStackManager;
        private ITransactionMetricNameMaker _transactionMetricNameMaker;
        private IPathHashMaker _pathHashMaker;
        private ICatHeaderHandler _catHeaderHandler;


        private ISyntheticsHeaderHandler _syntheticsHeaderHandler;
        private ITransactionFinalizer _transactionFinalizer;
        private IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
        private IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
        private IConfigurationService _configurationService;
        private IAgentHealthReporter _agentHealthReporter;

        [SetUp]
        public void SetUp()
        {
            _callStackManager = Mock.Create<ICallStackManager>();
            _transaction = Mock.Create<ITransaction>();
            var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => _transaction.CallStackManager).Returns(_callStackManager);
            Mock.Arrange(() => _transaction.TransactionSegmentState).Returns(transactionSegmentState);

            // grr.  mocking is stupid
            Mock.Arrange(() => transactionSegmentState.CallStackPush(Arg.IsAny<Segment>()))
                .DoInstead<Segment>((segment) => _callStackManager.Push(segment.UniqueId));
            Mock.Arrange(() => transactionSegmentState.CallStackPop(Arg.IsAny<Segment>(), Arg.IsAny<bool>()))
                .DoInstead<Segment>((segment) => _callStackManager.TryPop(segment.UniqueId, segment.ParentUniqueId));
            Mock.Arrange(() => transactionSegmentState.ParentSegmentId()).Returns(_callStackManager.TryPeek);

            _transactionService = Mock.Create<ITransactionService>();
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(_transaction);

            _transactionTransformer = Mock.Create<ITransactionTransformer>();

            var threadPoolStatic = Mock.Create<IThreadPoolStatic>();
            Mock.Arrange(() => threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>()))
                .DoInstead<WaitCallback>(callback => callback(null));
            Mock.Arrange(() => threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>(), Arg.IsAny<object>()))
                .DoInstead<WaitCallback, object>((callback, state) => callback(state));

            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            _pathHashMaker = Mock.Create<IPathHashMaker>();
            _catHeaderHandler = Mock.Create<ICatHeaderHandler>();
            _syntheticsHeaderHandler = Mock.Create<ISyntheticsHeaderHandler>();
            _transactionFinalizer = Mock.Create<ITransactionFinalizer>();
            _browserMonitoringPrereqChecker = Mock.Create<IBrowserMonitoringPrereqChecker>();
            _browserMonitoringScriptMaker = Mock.Create<IBrowserMonitoringScriptMaker>();
            _configurationService = Mock.Create<IConfigurationService>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            _agentWrapperApi = new AgentWrapperApi(_transactionService, Mock.Create<ITimerFactory>(), _transactionTransformer, threadPoolStatic, _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, _syntheticsHeaderHandler, _transactionFinalizer, _browserMonitoringPrereqChecker, _browserMonitoringScriptMaker, _configurationService, _agentHealthReporter);
        }

        private class CallStackManagerFactory : ICallStackManagerFactory
        {
            private readonly ICallStackManager _callStackManager;

            public CallStackManagerFactory(ICallStackManager callStackManager)
            {
                _callStackManager = callStackManager;
            }

            public ICallStackManager CreateCallStackManager()
            {
                return _callStackManager;
            }
        }

        #region EndTransaction

        [Test]
        public void EndTransaction_DoesNotCallsTransactionTransformer_IfThereIsStillWorkToDo()
        {
            Mock.Arrange(() => _transaction.UnitOfWorkCount).Returns(1);

            _agentWrapperApi.CurrentTransaction.End();

            Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<ITransaction>()), Occurs.Never());
        }

        [Test]
        public void EndTransaction_CallsTransactionTransformer_WithBuiltImmutableTransaction()
        {
            _agentWrapperApi.CurrentTransaction.End();

            Mock.Assert(() => _transactionTransformer.Transform(_transaction));
        }

        [Test]
        public void EndTransaction_CallsTransactionBuilderFinalizer()
        {
            _agentWrapperApi.CurrentTransaction.End();

            Mock.Assert(() => _transactionFinalizer.Finish(_transaction));
        }

        #endregion EndTransaction

        #region Transaction metadata

        [Test]
        public void IgnoreTranasction_IgnoresTranaction()
        {
            _agentWrapperApi.CurrentTransaction.Ignore();

            Mock.Assert(() => _transaction.Ignore());
        }

        [Test]
        public void SetWebTransactionName_SetsWebTransactionName()
        {
            const int priority = 2;

            var addedTransactionName = (ITransactionName)null;
            Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
                .DoInstead<ITransactionName, int>((name, _) => addedTransactionName = name);

            _agentWrapperApi.CurrentTransaction.SetWebTransactionName(WebTransactionType.MVC, "foo", priority);

            Assert.NotNull(addedTransactionName);
            var webTransactionName = addedTransactionName as WebTransactionName;
            Assert.NotNull(webTransactionName);
            NrAssert.Multiple(
                () => Assert.AreEqual("MVC", webTransactionName.Category),
                () => Assert.AreEqual("foo", webTransactionName.Name)
                );
        }

        [Test]
        public void SetWebTransactionNameFromPath_SetsUriTransactionName()
        {
            const int priority = 1;

            var addedTransactionName = (ITransactionName)null;
            Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
                .DoInstead<ITransactionName, int>((name, _) => addedTransactionName = name);

            _agentWrapperApi.CurrentTransaction.SetWebTransactionNameFromPath(WebTransactionType.MVC, "some/path");

            Assert.NotNull(addedTransactionName);
            var webTransactionName = addedTransactionName as UriTransactionName;
            Assert.NotNull(webTransactionName);
            Assert.AreEqual("some/path", webTransactionName.Uri);
        }

        [Test]
        public void SetMessageBrokerTransactionName_SetsMessageBrokerTransactionName()
        {
            const int priority = 2;

            var addedTransactionName = (ITransactionName)null;
            Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
                .DoInstead<ITransactionName, int>((name, _) => addedTransactionName = name);

            _agentWrapperApi.CurrentTransaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "broker", "dest", priority);

            Assert.NotNull(addedTransactionName);
            var webTransactionName = addedTransactionName as MessageBrokerTransactionName;
            Assert.NotNull(webTransactionName);
            NrAssert.Multiple(
                () => Assert.AreEqual("Topic", webTransactionName.DestinationType),
                () => Assert.AreEqual("broker", webTransactionName.BrokerVendorName),
                () => Assert.AreEqual("dest", webTransactionName.Destination)
                );
        }

        [Test]
        public void SetOtherTransactionName_SetsOtherTransactionName()
        {
            const int priority = 2;

            var addedTransactionName = (ITransactionName)null;
            Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
                .DoInstead<ITransactionName, int>((name, _) => addedTransactionName = name);

            _agentWrapperApi.CurrentTransaction.SetOtherTransactionName("cat", "foo", priority);

            Assert.NotNull(addedTransactionName);
            var webTransactionName = addedTransactionName as OtherTransactionName;
            Assert.NotNull(webTransactionName);
            NrAssert.Multiple(
                () => Assert.AreEqual("cat", webTransactionName.Category),
                () => Assert.AreEqual("foo", webTransactionName.Name)
                );
        }

        [Test]
        public void SetCustomTransactionName_SetsCustomTransactionName()
        {
            const int priority = 2;

            var addedTransactionName = (ITransactionName)null;
            Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
                .DoInstead<ITransactionName, int>((name, _) => addedTransactionName = name);

            _agentWrapperApi.CurrentTransaction.SetCustomTransactionName("foo", priority);

            Assert.NotNull(addedTransactionName);
            var webTransactionName = addedTransactionName as CustomTransactionName;
            Assert.NotNull(webTransactionName);
            Assert.AreEqual("foo", webTransactionName.Name);
        }

        [Test]
        public void SetTransactionUri_SetsTransactionUri()
        {
            _agentWrapperApi.CurrentTransaction.SetUri("foo");

            Mock.Assert(() => _transaction.TransactionMetadata.SetUri("foo"));
        }

        [Test]
        public void SetTransactionOriginalUri_SetsTransactionOriginalUri()
        {
            _agentWrapperApi.CurrentTransaction.SetOriginalUri("foo");

            Mock.Assert(() => _transaction.TransactionMetadata.SetOriginalUri("foo"));
        }

        [Test]
        public void SetTransactionPath_SetsTransactionPath()
        {
            _agentWrapperApi.CurrentTransaction.SetPath("foo");

            Mock.Assert(() => _transaction.TransactionMetadata.SetPath("foo"));
        }

        [Test]
        public void SetTransactionReferrerUri_SetsTransactionReferrerUri()
        {
            _agentWrapperApi.CurrentTransaction.SetReferrerUri("foo");

            Mock.Assert(() => _transaction.TransactionMetadata.SetReferrerUri("foo"));
        }

        [Test]
        public void SetTransactionQueueTime_SetsTransactionQueueTime()
        {
            _agentWrapperApi.CurrentTransaction.SetQueueTime(TimeSpan.FromSeconds(4));

            Mock.Assert(() => _transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(4)));
        }

        [Test]
        public void SetTransactionRequestParameters_SetsTransactionRequestParameters_ForRequestBucket()
        {
            var parameters = new Dictionary<string, string> { { "key", "value" } };
            _agentWrapperApi.CurrentTransaction.SetRequestParameters(parameters, RequestParameterBucket.RequestParameters);

            Mock.Assert(() => _transaction.TransactionMetadata.AddRequestParameter("key", "value"));
        }

        [Test]
        public void SetTransactionRequestParameters_SetsTransactionRequestParameters_ForServiceBucket()
        {
            var parameters = new Dictionary<string, string> { { "key", "value" } };
            _agentWrapperApi.CurrentTransaction.SetRequestParameters(parameters, RequestParameterBucket.ServiceRequest);

            Mock.Assert(() => _transaction.TransactionMetadata.AddServiceParameter("key", "value"));
        }

        [Test]
        public void SetTransactionHttpResponseStatusCode_SetsTransactionHttpResponseStatusCode()
        {
            _agentWrapperApi.CurrentTransaction.SetHttpResponseStatusCode(1, 2);

            Mock.Assert(() => _transaction.TransactionMetadata.SetHttpResponseStatusCode(1, 2));
        }

        #endregion Transaction metadata

        #region Segments

        [Test]
        public void StartTransactionSegment_ReturnsAnOpaqueSimpleSegmentBuilder()
        {
            var expectedParentId = 1;
            Mock.Arrange(() => _callStackManager.TryPeek()).Returns(expectedParentId);

            var invocationTarget = new object();
            var method = new Method(typeof(string), "methodName", "parameterTypeNames");
            var methodCall = new MethodCall(method, invocationTarget, new object[0]);
            var opaqueSegment = _agentWrapperApi.CurrentTransaction.StartTransactionSegment(methodCall, "foo");
            Assert.NotNull(opaqueSegment);

            var segment = opaqueSegment as TypedSegment<SimpleSegmentData>;
            Assert.NotNull(segment);

            var immutableSegment = segment as TypedSegment<SimpleSegmentData>;
            NrAssert.Multiple(
                () => Assert.NotNull(immutableSegment.UniqueId),
                () => Assert.AreEqual(expectedParentId, immutableSegment.ParentUniqueId),
                () => Assert.AreEqual("foo", immutableSegment.TypedData.Name),
                () => Assert.AreEqual("System.String", immutableSegment.MethodCallData.TypeName),
                () => Assert.AreEqual("methodName", immutableSegment.MethodCallData.MethodName),
                () => Assert.AreEqual(RuntimeHelpers.GetHashCode(invocationTarget), immutableSegment.MethodCallData.InvocationTargetHashCode)
                );
        }

        [Test]
        public void StartTransactionSegment_PushesNewSegmentUniqueIdToCallStack()
        {
            var pushedUniqueId = (object)null;
            Mock.Arrange(() => _callStackManager.Push(Arg.IsAny<int>()))
                .DoInstead<object>(pushed => pushedUniqueId = pushed);

            var opaqueSegment = _agentWrapperApi.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0]), "foo");
            Assert.NotNull(opaqueSegment);

            var segment = opaqueSegment as TypedSegment<SimpleSegmentData>;
            Assert.NotNull(segment);

            var immutableSegment = segment as TypedSegment<SimpleSegmentData>;
            Assert.AreEqual(pushedUniqueId, immutableSegment.UniqueId);
        }

        [Test]
        public void StartExternalSegment_Throws_IfUriIsNotAbsolute()
        {
            var uri = new Uri("/test", UriKind.Relative);
            NrAssert.Throws<ArgumentException>(() => _agentWrapperApi.CurrentTransaction.StartExternalRequestSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0]), uri, "GET"));
        }

        #endregion Segments

        #region EndSegment

        [Test]
        public void EndSegment_RemovesSegmentFromCallStack()
        {
            var opaqueSegment = _agentWrapperApi.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0]), "foo");
            var segment = opaqueSegment as Segment;
            var expectedUniqueId = segment.UniqueId;
            var expectedParentId = segment.ParentUniqueId;

            segment.End();

            Mock.Assert(() => _callStackManager.TryPop(expectedUniqueId, expectedParentId), Occurs.Once());
        }


        #endregion EndSegment

        #region NoticeError

        [Test]
        public void NoticeError_SendsErrorDataToTransactionBuilder()
        {
            var now = DateTime.UtcNow;
            var actualErrorData = null as ErrorData?;
            Mock.Arrange(() => _transaction.TransactionMetadata.AddExceptionData(Arg.IsAny<ErrorData>()))
                .DoInstead<ErrorData>(data => actualErrorData = data);

            var exception = NotNewRelic.ExceptionBuilder.BuildException("My message");
            _agentWrapperApi.CurrentTransaction.NoticeError(exception);

            Assert.NotNull(actualErrorData);
            NrAssert.Multiple(
                () => Assert.AreEqual("My message", actualErrorData.Value.ErrorMessage),
                () => Assert.AreEqual("System.Exception", actualErrorData.Value.ErrorTypeName),
                () => Assert.IsTrue(actualErrorData.Value.StackTrace?.Contains("NotNewRelic.ExceptionBuilder.BuildException") == true),
                () => Assert.IsTrue(actualErrorData.Value.NoticedAt >= now && actualErrorData.Value.NoticedAt < now.AddMinutes(1))
                );
        }

        [Test]
        public void NoticeError_SendsAllErrorDataToTransactionBuilder()
        {
            var exception = NotNewRelic.ExceptionBuilder.BuildException("My message");

            _agentWrapperApi.CurrentTransaction.NoticeError(exception);
            _agentWrapperApi.CurrentTransaction.NoticeError(exception);

            Mock.Assert(() => _transaction.TransactionMetadata.AddExceptionData(Arg.IsAny<ErrorData>()), Occurs.Exactly(2));
        }

        #endregion NoticeError

        #region inbound CAT request - outbound CAT response

        [Test]
        public void ProcessInboundRequest_SetsReferrerCrossProcessId()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
                .Returns("referrerProcessId");
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
            var headers = new Dictionary<string, string>();

            _agentWrapperApi.ProcessInboundRequest(headers);

            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId"));
        }

        [Test]
        public void ProcessInboundRequest_SetsTransactionData()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
                .Returns("referrerProcessId");
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
            var headers = new Dictionary<string, string>();

            _agentWrapperApi.ProcessInboundRequest(headers);

            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId"));
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash"));
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referrerTransactionGuid"));
        }

        [Test]
        public void ProcessInboundRequest_SetsTransactionData_WithContentLength()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
                .Returns("referrerProcessId");
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
            var headers = new Dictionary<string, string>();

            _agentWrapperApi.ProcessInboundRequest(headers, 200);

            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId"));
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(200));
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash"));
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referrerTransactionGuid"));
        }

        [Test]
        public void ProcessInboundRequest_DoesNotSetTransactionDataIfCrossProcessIdAlreadyExists()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns("referrerProcessId");
            var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
                .Returns("referrerProcessId");
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns("referrerProcessId");
            var headers = new Dictionary<string, string>();

            _agentWrapperApi.ProcessInboundRequest(headers);

            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(Arg.IsAny<string>()), Occurs.Never());
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId"), Occurs.Never());
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(Arg.IsAny<int>()), Occurs.Never());
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash"), Occurs.Never());
            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referrerTransactionGuid"), Occurs.Never());
        }

        [Test]
        public void ProcessInboundRequest_DoesNotThrow_IfContentLengthIsNull()
        {
            var headers = new Dictionary<string, string>();
            _agentWrapperApi.ProcessInboundRequest(headers, null);

            // Simply not throwing is all this test needs to check for
        }

        [Test]
        public void GetResponseMetadata_ReturnsEmpty_IfNoCurrentTransaction()
        {
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((ITransaction)null);

            var headers = _agentWrapperApi.CurrentTransaction.GetResponseMetadata();

            Assert.NotNull(headers);
            Assert.IsEmpty(headers);
        }

        [Test]
        public void GetResponseMetadata_ReturnsEmpty_IfNoReferrerCrossProcessId()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

            var headers = _agentWrapperApi.CurrentTransaction.GetResponseMetadata();

            Assert.NotNull(headers);
            Assert.IsEmpty(headers);
        }

        [Test]
        public void GetResponseMetadata_CallsSetPathHashWithResultsFromPathHashMaker()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

            _agentWrapperApi.CurrentTransaction.GetResponseMetadata();

            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
        }

        [Test]
        public void GetResponseMetadata_ReturnsCatHeadersFromCatHeaderHandler()
        {
            var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            Mock.Arrange(() => _catHeaderHandler.TryGetOutboundResponseHeaders(Arg.IsAny<ITransaction>(), Arg.IsAny<TransactionMetricName>())).Returns(catHeaders);

            var headers = _agentWrapperApi.CurrentTransaction.GetResponseMetadata().ToDictionary();

            Assert.NotNull(headers);
            NrAssert.Multiple(
                () => Assert.AreEqual("value1", headers["key1"]),
                () => Assert.AreEqual("value2", headers["key2"])
                );
        }

        #endregion inbound CAT request - outbound CAT response

        #region outbound CAT request - inbound CAT response

        [Test]
        public void ProcessInboundResponse_SetsSegmentCatResponseData_IfCatHeaderHandlerReturnsData()
        {
            var expectedCatResponseData = new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundResponseHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(expectedCatResponseData);

            var headers = new Dictionary<string, string>();
            var segmentBuilder = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));
            _agentWrapperApi.CurrentTransaction.ProcessInboundResponse(headers, segmentBuilder);

            var builtSegment = segmentBuilder as TypedSegment<ExternalSegmentData>;
            Assert.AreEqual(builtSegment.TypedData.CrossApplicationResponseData, expectedCatResponseData);
            Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Exactly(1));
        }

        [Test]
        public void ProcessInboundResponse_DoesNotThrow_WhenNoCurrentTransaction()
        {
            Transaction transaction = null;
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(transaction);

            var headers = new Dictionary<string, string>();
            var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));

            _agentWrapperApi.CurrentTransaction.ProcessInboundResponse(headers, segment);

            Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());
        }

        [Test]
        public void ProcessInboundResponse_NullFromTryDecode()
        {
            CrossApplicationResponseData expectedCatResponseData = null;
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundResponseHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(expectedCatResponseData);

            var headers = new Dictionary<string, string>();
            var segmentBuilder = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));
            _agentWrapperApi.CurrentTransaction.ProcessInboundResponse(headers, segmentBuilder);

            var builtSegment = segmentBuilder as TypedSegment<ExternalSegmentData>;
            Assert.AreEqual(builtSegment.TypedData.CrossApplicationResponseData, expectedCatResponseData);
            Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());
        }

        [Test]
        public void ProcessInboundResponse_SetsSegmentCatResponseData()
        {
            var expectedCatResponseData = new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundResponseHeaders(Arg.IsAny<IDictionary<string, string>>()))
                .Returns(expectedCatResponseData);

            var headers = new Dictionary<string, string>();
            var segmentBuilder = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));
            _agentWrapperApi.CurrentTransaction.ProcessInboundResponse(headers, segmentBuilder);

            var builtSegment = segmentBuilder as TypedSegment<ExternalSegmentData>;
            Assert.AreEqual(builtSegment.TypedData.CrossApplicationResponseData, expectedCatResponseData);
            Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Exactly(1));
        }

        [Test]
        public void ProcessInboundResponse_DoesNotThrow_IfSegmentIsNull()
        {
            var headers = new Dictionary<string, string>();
            _agentWrapperApi.CurrentTransaction.ProcessInboundResponse(headers, null);
            Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());

            // Simply not throwing is all this test needs to check for
        }

        [Test]
        public void GetRequestMetadata_ReturnsEmpty_IfNoCurrentTransaction()
        {
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((ITransaction)null);

            var headers = _agentWrapperApi.CurrentTransaction.GetRequestMetadata();

            Assert.NotNull(headers);
            Assert.IsEmpty(headers);
        }

        [Test]
        public void GetRequestMetadata_CallsSetPathHashWithResultsFromPathHashMaker()
        {
            var transactionName = new WebTransactionName("a", "b");
            Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

            _agentWrapperApi.CurrentTransaction.GetRequestMetadata();

            Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
        }

        [Test]
        public void GetRequestMetadata_ReturnsCatHeadersFromCatHeaderHandler()
        {
            var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<ITransaction>())).Returns(catHeaders);

            var headers = _agentWrapperApi.CurrentTransaction.GetRequestMetadata().ToDictionary();

            Assert.NotNull(headers);
            NrAssert.Multiple(
                () => Assert.AreEqual("value1", headers["key1"]),
                () => Assert.AreEqual("value2", headers["key2"])
                );
        }

        #endregion outbound CAT request - inbound CAT response
    }
}

// In order to generate a stack trace for testing we need a frame that is not in a NewRelic.* namespace (NewRelic frames are omitted from stack traces)
namespace NotNewRelic
{
    public static class ExceptionBuilder
    {
        public static Exception BuildException(string message)
        {
            try
            {
                throw new Exception(message);
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}
