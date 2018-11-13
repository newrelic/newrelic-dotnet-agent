using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Telerik.JustMock;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;


namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	[TestFixture]
	public class AgentWrapperApiTests
	{
		[NotNull] private ITransaction _transaction;

		[NotNull] private ITransactionService _transactionService;

		[NotNull] private IAgentWrapperApi _agentWrapperApi;

		[NotNull] private ITransactionTransformer _transactionTransformer;

		[NotNull] private ICallStackManager _callStackManager;

		[NotNull] private ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull] private IPathHashMaker _pathHashMaker;

		[NotNull] private ICatHeaderHandler _catHeaderHandler;
		[NotNull] private IDistributedTracePayloadHandler _distributedTracePayloadHandler;


		[NotNull] private ISyntheticsHeaderHandler _syntheticsHeaderHandler;

		[NotNull] private ITransactionFinalizer _transactionFinalizer;

		[NotNull] private IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;

		[NotNull] private IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		[NotNull] private IConfigurationService _configurationService;

		[NotNull] private IAgentHealthReporter _agentHealthReporter;

		private IAgentTimerService _agentTimerService;

		private const string DistributedTraceHeaderName = "Newrelic";

		[SetUp]
		public void SetUp()
		{
			_callStackManager = Mock.Create<ICallStackManager>();
			_transaction = Mock.Create<ITransaction>();
			var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
			Mock.Arrange(() => _transaction.CallStackManager).Returns(_callStackManager);
			Mock.Arrange(() => _transaction.GetTransactionSegmentState()).Returns(transactionSegmentState);

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
			Mock.Arrange(() => threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>(), Arg.IsAny<Object>()))
				.DoInstead<WaitCallback, Object>((callback, state) => callback(state));

			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			_pathHashMaker = Mock.Create<IPathHashMaker>();
			_catHeaderHandler = Mock.Create<ICatHeaderHandler>();
			_syntheticsHeaderHandler = Mock.Create<ISyntheticsHeaderHandler>();
			_transactionFinalizer = Mock.Create<ITransactionFinalizer>();
			_browserMonitoringPrereqChecker = Mock.Create<IBrowserMonitoringPrereqChecker>();
			_browserMonitoringScriptMaker = Mock.Create<IBrowserMonitoringScriptMaker>();
			_configurationService = Mock.Create<IConfigurationService>();
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_agentTimerService = Mock.Create<IAgentTimerService>();

			_distributedTracePayloadHandler = Mock.Create<DistributedTracePayloadHandler>(Behavior.CallOriginal, _configurationService, _agentHealthReporter, new AdaptiveSampler());

			_agentWrapperApi = new AgentWrapperApi(_transactionService, Mock.Create<ITimerFactory>(), _transactionTransformer, threadPoolStatic, _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, _distributedTracePayloadHandler, _syntheticsHeaderHandler, _transactionFinalizer, _browserMonitoringPrereqChecker, _browserMonitoringScriptMaker, _configurationService, _agentHealthReporter, _agentTimerService);
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
			Mock.Arrange(() => _transaction.NoticeUnitOfWorkEnds()).Returns(1);

			_agentWrapperApi.CurrentTransactionWrapperApi.End();

			Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<ITransaction>()), Occurs.Never());
		}

		[Test]
		public void EndTransaction_CallsTransactionTransformer_WithBuiltImmutableTransaction()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.End();

			Mock.Assert(() => _transactionTransformer.Transform(_transaction));
		}

		[Test]
		public void EndTransaction_CallsTransactionBuilderFinalizer()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.End();

			Mock.Assert(() => _transactionFinalizer.Finish(_transaction));
		}

		#endregion EndTransaction

		#region Transaction metadata

		[Test]
		public void IgnoreTranasction_IgnoresTranaction()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.Ignore();

			Mock.Assert(() => _transaction.Ignore());
		}

		[Test]
		public void SetWebTransactionName_SetsWebTransactionName()
		{
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;

			var addedTransactionName = (ITransactionName) null;
			Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
				.DoInstead<ITransactionName, TransactionNamePriority>((name, _) => addedTransactionName = name);

			_agentWrapperApi.CurrentTransactionWrapperApi.SetWebTransactionName(WebTransactionType.MVC, "foo", priority);

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
			const TransactionNamePriority priority = TransactionNamePriority.Uri;

			var addedTransactionName = (ITransactionName) null;
			Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
				.DoInstead<ITransactionName, TransactionNamePriority>((name, _) => addedTransactionName = name);

			_agentWrapperApi.CurrentTransactionWrapperApi.SetWebTransactionNameFromPath(WebTransactionType.MVC, "some/path");

			Assert.NotNull(addedTransactionName);
			var webTransactionName = addedTransactionName as UriTransactionName;
			Assert.NotNull(webTransactionName);
			Assert.AreEqual("some/path", webTransactionName.Uri);
		}

		[Test]
		public void SetMessageBrokerTransactionName_SetsMessageBrokerTransactionName()
		{
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;

			var addedTransactionName = (ITransactionName) null;
			Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
				.DoInstead<ITransactionName, TransactionNamePriority>((name, _) => addedTransactionName = name);

			_agentWrapperApi.CurrentTransactionWrapperApi.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "broker", "dest", priority);

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
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;

			var addedTransactionName = (ITransactionName) null;
			Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
				.DoInstead<ITransactionName, TransactionNamePriority>((name, _) => addedTransactionName = name);

			_agentWrapperApi.CurrentTransactionWrapperApi.SetOtherTransactionName("cat", "foo", priority);

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
			const TransactionNamePriority priority = TransactionNamePriority.StatusCode;

			var addedTransactionName = (ITransactionName) null;
			Mock.Arrange(() => _transaction.CandidateTransactionName.TrySet(Arg.IsAny<ITransactionName>(), priority))
				.DoInstead<ITransactionName, TransactionNamePriority>((name, _) => addedTransactionName = name);

			_agentWrapperApi.CurrentTransactionWrapperApi.SetCustomTransactionName("foo", priority);

			Assert.NotNull(addedTransactionName);
			var webTransactionName = addedTransactionName as CustomTransactionName;
			Assert.NotNull(webTransactionName);
			Assert.AreEqual("foo", webTransactionName.Name);
		}

		[Test]
		public void SetTransactionUri_SetsTransactionUri()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.SetUri("foo");

			Mock.Assert(() => _transaction.TransactionMetadata.SetUri("foo"));
		}

		[Test]
		public void SetTransactionOriginalUri_SetsTransactionOriginalUri()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.SetOriginalUri("foo");

			Mock.Assert(() => _transaction.TransactionMetadata.SetOriginalUri("foo"));
		}

		[Test]
		public void SetTransactionReferrerUri_SetsTransactionReferrerUri()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.SetReferrerUri("foo");

			Mock.Assert(() => _transaction.TransactionMetadata.SetReferrerUri("foo"));
		}

		[Test]
		public void SetTransactionQueueTime_SetsTransactionQueueTime()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.SetQueueTime(TimeSpan.FromSeconds(4));

			Mock.Assert(() => _transaction.TransactionMetadata.SetQueueTime(TimeSpan.FromSeconds(4)));
		}

		[Test]
		public void SetTransactionRequestParameters_SetsTransactionRequestParameters_ForRequestBucket()
		{
			var parameters = new Dictionary<string, string> { { "key", "value" } };
			_agentWrapperApi.CurrentTransactionWrapperApi.SetRequestParameters(parameters);

			Mock.Assert(() => _transaction.TransactionMetadata.AddRequestParameter("key", "value"));
		}

		[Test]
		public void SetTransactionRequestParameters_SetMultipleRequestParameters()
		{
			var parameters = new Dictionary<string, string> { { "firstName", "Jane" }, { "lastName", "Doe" } };
			_agentWrapperApi.CurrentTransactionWrapperApi.SetRequestParameters(parameters);

			Mock.Assert(() => _transaction.TransactionMetadata.AddRequestParameter("firstName", "Jane"));
			Mock.Assert(() => _transaction.TransactionMetadata.AddRequestParameter("lastName", "Doe"));
		}

		[Test]
		public void SetTransactionHttpResponseStatusCode_SetsTransactionHttpResponseStatusCode()
		{
			_agentWrapperApi.CurrentTransactionWrapperApi.SetHttpResponseStatusCode(1, 2);

			Mock.Assert(() => _transaction.TransactionMetadata.SetHttpResponseStatusCode(1, 2));
		}

		#endregion Transaction metadata

		#region Segments

		[Test]
		public void StartTransactionSegment_ReturnsAnOpaqueSimpleSegmentBuilder()
		{
			var expectedParentId = 1;
			Mock.Arrange(() => _callStackManager.TryPeek()).Returns(expectedParentId);

			var invocationTarget = new Object();
			var method = new Method(typeof(string), "methodName", "parameterTypeNames");
			var methodCall = new MethodCall(method, invocationTarget, new Object[0]);
			var opaqueSegment = _agentWrapperApi.CurrentTransactionWrapperApi.StartTransactionSegment(methodCall, "foo");
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
			var pushedUniqueId = (Object) null;
			Mock.Arrange(() => _callStackManager.Push(Arg.IsAny<int>()))
				.DoInstead<Object>(pushed => pushedUniqueId = pushed);

			var opaqueSegment = _agentWrapperApi.CurrentTransactionWrapperApi.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), "foo");
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
			NrAssert.Throws<ArgumentException>(() => _agentWrapperApi.CurrentTransactionWrapperApi.StartExternalRequestSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), uri, "GET"));
		}

		#endregion Segments

		#region EndSegment

		[Test]
		public void EndSegment_RemovesSegmentFromCallStack()
		{
			var opaqueSegment = _agentWrapperApi.CurrentTransactionWrapperApi.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), "foo");
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
			_agentWrapperApi.CurrentTransactionWrapperApi.NoticeError(exception);

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

			_agentWrapperApi.CurrentTransactionWrapperApi.NoticeError(exception);
			_agentWrapperApi.CurrentTransactionWrapperApi.NoticeError(exception);

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

			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP);

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

			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP);

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

			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP, 200);

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

			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP);

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
			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.Unknown, null);

			// Simply not throwing is all this test needs to check for
		}

		[Test]
		public void ProcessInboundRequest__ReportsSupportabilityMetric_NullPayload()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			
			var headers = new Dictionary<string, string>();
			headers[DistributedTraceHeaderName] = null;
			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTPS);

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull(), Occurs.Once());
		}


		[Test]
		public void GetResponseMetadata_ReturnsEmpty_IfNoCurrentTransaction()
		{
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((ITransaction) null);

			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetResponseMetadata();

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

			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetResponseMetadata();

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

			_agentWrapperApi.CurrentTransactionWrapperApi.GetResponseMetadata();

			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
		}

		[Test]
		public void GetResponseMetadata_ReturnsCatHeadersFromCatHeaderHandler()
		{
			var catHeaders = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}};
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundResponseHeaders(Arg.IsAny<ITransaction>(), Arg.IsAny<TransactionMetricName>())).Returns(catHeaders);

			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetResponseMetadata().ToDictionary();

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
			_agentWrapperApi.CurrentTransactionWrapperApi.ProcessInboundResponse(headers, segmentBuilder);

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

			_agentWrapperApi.CurrentTransactionWrapperApi.ProcessInboundResponse(headers, segment);

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
			_agentWrapperApi.CurrentTransactionWrapperApi.ProcessInboundResponse(headers, segmentBuilder);

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
			_agentWrapperApi.CurrentTransactionWrapperApi.ProcessInboundResponse(headers, segmentBuilder);

			var builtSegment = segmentBuilder as TypedSegment<ExternalSegmentData>;
			Assert.AreEqual(builtSegment.TypedData.CrossApplicationResponseData, expectedCatResponseData);
			Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Exactly(1));
		}

		[Test]
		public void ProcessInboundResponse_DoesNotThrow_IfSegmentIsNull()
		{
			var headers = new Dictionary<string, string>();
			_agentWrapperApi.CurrentTransactionWrapperApi.ProcessInboundResponse(headers, null);
			Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());

			// Simply not throwing is all this test needs to check for
		}

		[Test]
		public void GetRequestMetadata_ReturnsEmpty_IfNoCurrentTransaction()
		{
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((ITransaction) null);

			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetRequestMetadata();

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

			_agentWrapperApi.CurrentTransactionWrapperApi.GetRequestMetadata();

			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
		}

		[Test]
		public void GetRequestMetadata_ReturnsCatHeadersFromCatHeaderHandler()
		{
			var catHeaders = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}};
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<ITransaction>())).Returns(catHeaders);

			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetRequestMetadata().ToDictionary();

			Assert.NotNull(headers);
			NrAssert.Multiple(
				() => Assert.AreEqual("value1", headers["key1"]),
				() => Assert.AreEqual("value2", headers["key2"])
			);
		}

		#endregion outbound CAT request - inbound CAT response

		#region Distributed Trace

		private static readonly string _accountId = "acctid";
		private static readonly string _appId  = "appid";
		private static readonly string _guid  = "guid";
		private static readonly float _priority = .3f;
		private static readonly bool _sampled = true;
		private static readonly string _traceId  = "traceid";
		private static readonly string _trustKey = "trustedkey";
		private static readonly string _type  = "typeapp";
		private static readonly string _transactionId = "transactionId";

		private readonly DistributedTracePayload _distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(_type, _accountId, _appId, _guid, _traceId, _trustKey, _priority, _sampled, DateTime.UtcNow, _transactionId);

		[Test]
		public void GetRequestMetadata_DoesNotReturnsCatHeaders_IfDistributedTraceEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<ITransaction>())).Returns(catHeaders);

			// Act
			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetRequestMetadata().ToDictionary();

			// Assert
			const string NewRelicIdHttpHeader = "X-NewRelic-ID";
			const string TransactionDataHttpHeader = "X-NewRelic-Transaction";
			const string AppDataHttpHeader = "X-NewRelic-App-Data";

			Assert.That(headers, Does.Not.ContainKey(NewRelicIdHttpHeader));
			Assert.That(headers, Does.Not.ContainKey(TransactionDataHttpHeader));
			Assert.That(headers, Does.Not.ContainKey(AppDataHttpHeader));

			Mock.Assert(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<ITransaction>()), Occurs.Never());
		}

		[Test]
		public void GetRequestMetadata_ReturnsDistributedTraceHeadersFromDTPayloadHandler_IfDistributedTraceIsEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var distributedTraceHeaders = Mock.Create<IDistributedTraceApiModel>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<ITransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			// Act
			var headers = _agentWrapperApi.CurrentTransactionWrapperApi.GetRequestMetadata().ToDictionary();

			// Assert
			NrAssert.Multiple(
				() => Assert.That(headers, Has.Exactly(1).Items),
				() => Assert.AreEqual(distributedTraceHeaders.HttpSafe(), headers[DistributedTraceHeaderName])
			);
		}

		[Test]
		public void ProcessInboundRequest_SetsDTMetadataAndNotCATMetadata_IfDistributedTraceIsEnabled()
		{
			// Arrange
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
			var headers = new Dictionary<string, string>() {{ DistributedTraceHeaderName, "encodedpayload"}};

			var transactionMetadata = new TransactionMetadata();
			
			Mock.Arrange(() => _transaction.TransactionMetadata).Returns(transactionMetadata);

			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			// Act
			_agentWrapperApi.ProcessInboundRequest(headers, TransportType.HTTP);

			// Assert
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(Arg.IsAny<string>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId(Arg.IsAny<string>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(Arg.IsAny<int>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash(Arg.IsAny<string>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid(Arg.IsAny<string>()), Occurs.Never());

			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceAccountId, _accountId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceAppId, _appId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceGuid, _guid);
			Assert.AreEqual(_transaction.TransactionMetadata.Priority, _priority);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceTraceId, _traceId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceTrustKey, _trustKey);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceType, _type);

			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash);
			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerProcessId);
			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerTripId);
		}

		[Test]
		public void ProcessInboundRequest_DoesNotSetTransactionMetadata_IfPayloadAlreadyAccepted()
		{
			// Arrange
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var transactionMetadata = new TransactionMetadata() { HasIncomingDistributedTracePayload = true };
			Mock.Arrange(() => _transaction.TransactionMetadata).Returns(transactionMetadata);

			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			// Act
			_agentWrapperApi.ProcessInboundRequest(new KeyValuePair<string, string>[] { }, TransportType.HTTP);

			// Assert
			Mock.Assert(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceAccountId, Occurs.Never());
		}

		[Test]
		public void ProcessInboundRequest_DoesNotSetTransactionData_IfOutgoingPayloadCreated()
		{
			// Arrange
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var transactionMetadata = new TransactionMetadata() { HasOutgoingDistributedTracePayload = true };
			Mock.Arrange(() => _transaction.TransactionMetadata).Returns(transactionMetadata);

			// Act
			_agentWrapperApi.ProcessInboundRequest(new KeyValuePair<string, string>[] { }, TransportType.HTTP);

			// Assert
			Mock.Assert(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>()), Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceAccountId, Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceAppId, Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceGuid, Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceTrustKey, Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceTraceId, Occurs.Never());
			Mock.Assert(() => _transaction.TransactionMetadata.DistributedTraceType, Occurs.Never());
		}

		[Test]
		public void AcceptDistributedTracePayload_ReportsSupportabilityMetric_IfAcceptCalledMultipleTimes()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var payload = string.Empty;
	Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);
			_agentWrapperApi.CurrentTransactionWrapperApi.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).CallOriginal();
			_agentWrapperApi.CurrentTransactionWrapperApi.AcceptDistributedTracePayload(payload, TransportType.HTTPS);


			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple(), Occurs.Once());
		}

		[Test]
		public void CreateDistributedTracePayload_ShouldReturnPayload_IfConfigIsTrue()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var distributedTraceHeaders = Mock.Create<IDistributedTraceApiModel>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<ITransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			var payload = _agentWrapperApi.CurrentTransactionWrapperApi.CreateDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.AreEqual(distributedTraceHeaders, payload)
			);
		}

		[Test]
		public void CreateDistributedTracePayload_ShouldNotReturnPayload_IfConfigIsFalse()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

			var distributedTraceHeaders = Mock.Create<IDistributedTraceApiModel>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<ITransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			var payload = _agentWrapperApi.CurrentTransactionWrapperApi.CreateDistributedTracePayload();

			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}
	

		[Test]
		public void AcceptDistributedTracePayload_ShouldAccept_IfConfigIsTrue()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var payload = string.Empty;
			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			Mock.Arrange(() => _transaction.TransactionMetadata).Returns(new TransactionMetadata());

			_agentWrapperApi.CurrentTransactionWrapperApi.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

			var metaData = _transaction.TransactionMetadata;
			NrAssert.Multiple(
				() => Assert.AreEqual(_accountId, metaData.DistributedTraceAccountId),
				() => Assert.AreEqual(_appId, metaData.DistributedTraceAppId),
				() => Assert.AreEqual(_guid, metaData.DistributedTraceGuid),
				() => Assert.AreEqual(_sampled, metaData.DistributedTraceSampled),
				() => Assert.AreEqual(_traceId, metaData.DistributedTraceTraceId),
				() => Assert.AreEqual(_transactionId, metaData.DistributedTraceTransactionId),
				() => Assert.AreEqual("HTTPS", metaData.DistributedTraceTransportType),
				() => Assert.AreEqual(_trustKey, metaData.DistributedTraceTrustKey),
				() => Assert.AreEqual(_type, metaData.DistributedTraceType),
				() => Assert.AreEqual(_priority, metaData.Priority)
			);
		}

		[Test]
		public void AcceptDistributedTracePayload_ShouldNotAccept_IfConfigIsFalse()
		{
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

			var payload = string.Empty;
			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			_agentWrapperApi.CurrentTransactionWrapperApi.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

			var metaData = _transaction.TransactionMetadata;
			NrAssert.Multiple(
				() => Assert.AreNotEqual(_accountId, metaData.DistributedTraceAccountId),
				() => Assert.AreNotEqual(_appId, metaData.DistributedTraceAppId),
				() => Assert.AreNotEqual(_guid, metaData.DistributedTraceGuid),
				() => Assert.AreNotEqual(_sampled, metaData.DistributedTraceSampled),
				() => Assert.AreNotEqual(_traceId, metaData.DistributedTraceTraceId),
				() => Assert.AreNotEqual(_transactionId, metaData.DistributedTraceTransactionId),
				() => Assert.AreNotEqual("HTTPS", metaData.DistributedTraceTransportType),
				() => Assert.AreNotEqual(_trustKey, metaData.DistributedTraceTrustKey),
				() => Assert.AreNotEqual(_type, metaData.DistributedTraceType),
				() => Assert.AreNotEqual(_priority, metaData.Priority)
			);
		}

		[TestCase("Newrelic", "payload", true)]
		[TestCase("NewRelic", "payload", true)]
		[TestCase("newrelic", "payload", true)]
		[TestCase("Fred", null, false)]
		public void TryGetDistributedTracePayload_ReturnsValidPayloadOrNull(string key, string expectedPayload, bool expectedResult)
		{
			var headers = new List<KeyValuePair<string, string>>();
			headers.Add(new KeyValuePair<string, string>(key, expectedPayload));

			var actualResult = _agentWrapperApi.TryGetDistributedTracePayloadFromHeaders(headers, out var actualPayload);

			NrAssert.Multiple(
				() => Assert.AreEqual(expectedPayload, actualPayload),
				() => Assert.AreEqual(expectedResult, actualResult)
			);
		}

		[Test]
		public void TryGetDistributedTracePayload_HeaderCollectionTests_NullHeader()
		{
			TryGetDistributedTracePayload_HeaderCollectionTests(null, null, false);
		}

		[Test]
		public void TryGetDistributedTracePayload_HeaderCollectionTests_EmptyHeader()
		{
			TryGetDistributedTracePayload_HeaderCollectionTests(new List<KeyValuePair<string, string>>(), null, false);
		}

		[Test]
		public void TryGetDistributedTracePayload_HeaderCollectionTests_OneItemWithoutDTHeader()
		{
			var oneItemWithoutDTHeader = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Fred", "Wilma") };

			TryGetDistributedTracePayload_HeaderCollectionTests(oneItemWithoutDTHeader, null, false);
		}

		[Test]
		public void TryGetDistributedTracePayload_HeaderCollectionTests_MultipleItemsWithDTHeader()
		{
			var multipleItemsWithDTHeader = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("Fred", "Wilma"),
				new KeyValuePair<string, string>("Barney", "Betty"),
				new KeyValuePair<string, string>(DistributedTraceHeaderName, "payload")
			};

			TryGetDistributedTracePayload_HeaderCollectionTests(multipleItemsWithDTHeader, "payload", true);
		}

		private void TryGetDistributedTracePayload_HeaderCollectionTests(List<KeyValuePair<string, string>> headers, string expectedPayload, bool expectedResult)
		{
			var actualResult = _agentWrapperApi.TryGetDistributedTracePayloadFromHeaders(headers, out var actualPayload);

			NrAssert.Multiple(
				() => Assert.AreEqual(expectedPayload, actualPayload),
				() => Assert.AreEqual(expectedResult, actualResult)
			);
		}

		#endregion Distributed Trace
	}
}

// In order to generate a stack trace for testing we need a frame that is not in a NewRelic.* namespace (NewRelic frames are omitted from stack traces)
namespace NotNewRelic
{
	public static class ExceptionBuilder
	{
		[NotNull]
		public static Exception BuildException([NotNull] string message)
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
