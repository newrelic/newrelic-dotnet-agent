using JetBrains.Annotations;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	[TestFixture]
	public class AgentWrapperApiTests
	{
		[NotNull] private IInternalTransaction _transaction;

		[NotNull] private ITransactionService _transactionService;

		[NotNull] private IAgent _agent;

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
		private IMetricNameService _metricNameService;

		private const string DistributedTraceHeaderName = "Newrelic";

		[SetUp]
		public void SetUp()
		{
			_callStackManager = Mock.Create<ICallStackManager>();
			_transaction = Mock.Create<IInternalTransaction>();
			var transactionSegmentState = Mock.Create<ITransactionSegmentState>();
			Mock.Arrange(() => _transaction.CallStackManager).Returns(_callStackManager);
			Mock.Arrange(() => _transaction.GetTransactionSegmentState()).Returns(transactionSegmentState);
			Mock.Arrange(() => _transaction.Finish()).Returns(true);

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
			_agentTimerService = Mock.Create<IAgentTimerService>();
			_metricNameService = new MetricNameService();
			var catMetrics = Mock.Create<ICATSupportabilityMetricCounters>();

			_distributedTracePayloadHandler = Mock.Create<DistributedTracePayloadHandler>(Behavior.CallOriginal, _configurationService, _agentHealthReporter, new AdaptiveSampler());

			_agent = new Agent(_transactionService, Mock.Create<ITimerFactory>(), _transactionTransformer, threadPoolStatic, _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, _distributedTracePayloadHandler, _syntheticsHeaderHandler, _transactionFinalizer, _browserMonitoringPrereqChecker, _browserMonitoringScriptMaker, _configurationService, _agentHealthReporter, _agentTimerService, _metricNameService, new TraceMetadataFactory(new AdaptiveSampler()), catMetrics);
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

			_agent.CurrentTransaction.End();

			Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<IInternalTransaction>()), Occurs.Never());
		}

		[Test]
		public void EndTransaction_CallsTransactionTransformer_WithBuiltImmutableTransaction()
		{
			SetupTransaction();
			Mock.Arrange(() => _transactionFinalizer.Finish(_transaction)).Returns(true);

			_agent.CurrentTransaction.End();

			Mock.Assert(() => _transactionTransformer.Transform(_transaction));
		}

		[Test]
		public void EndTransaction_DoesNotCallTransactionTransformer_IfTransactionWasNotFinished()
		{
			Mock.Arrange(() => _transactionFinalizer.Finish(_transaction)).Returns(false);

			_agent.CurrentTransaction.End();

			Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<IInternalTransaction>()), Occurs.Never());
		}

		[Test]
		public void EndTransaction_CallsTransactionBuilderFinalizer()
		{
			SetupTransaction();

			_agent.CurrentTransaction.End();

			Mock.Assert(() => _transactionFinalizer.Finish(_transaction));
		}

		[Test]
		public void EndTransaction_ShouldNotLogResponseTimeAlreadyCaptured()
		{
			Mock.Arrange(() => _transaction.TryCaptureResponseTime()).Returns(true);

			using (var logging = new TestUtilities.Logging())
			{
				_agent.CurrentTransaction.End();

				var foundResponseTimeAlreadyCapturedMessage = logging.HasMessageBeginingWith("Transaction has already captured the response time.");
				Assert.False(foundResponseTimeAlreadyCapturedMessage);
			}
		}

		[Test]
		public void EndTransaction_ShouldLogResponseTimeAlreadyCaptured()
		{
			SetupTransaction();

			using (var logging = new TestUtilities.Logging())
			{
				_agent.CurrentTransaction.End();
				_agent.CurrentTransaction.End();

				var foundResponseTimeAlreadyCapturedMessage = logging.HasMessageThatContains("Transaction has already captured the response time.");
				Assert.True(foundResponseTimeAlreadyCapturedMessage);
			}
		}

		#endregion EndTransaction

		#region Transaction metadata

		[Test]
		public void IgnoreTransaction_IgnoresTransaction()
		{
			_agent.CurrentTransaction.Ignore();

			Mock.Assert(() => _transaction.Ignore());
		}

		[Test]
		public void SetWebTransactionName_SetsWebTransactionName()
		{
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
			SetupTransaction();

			_agent.CurrentTransaction.SetWebTransactionName(WebTransactionType.MVC, "foo", priority);

			var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
			Assert.AreEqual("MVC", addedTransactionName.Category);
			Assert.AreEqual("foo", addedTransactionName.Name);
		}

		[Test]
		public void SetWebTransactionNameFromPath_SetsUriTransactionName()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetWebTransactionNameFromPath(WebTransactionType.MVC, "some/path");

			var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
			Assert.AreEqual("Uri/some/path", addedTransactionName.UnprefixedName);
			Assert.AreEqual(true, addedTransactionName.IsWeb);
		}

		[Test]
		public void SetMessageBrokerTransactionName_SetsMessageBrokerTransactionName()
		{
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
			SetupTransaction();

			_agent.CurrentTransaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "broker", "dest", priority);

			var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
			Assert.AreEqual("Message/broker/Topic/Named/dest", addedTransactionName.UnprefixedName);
			Assert.AreEqual(false, addedTransactionName.IsWeb);
		}

		[Test]
		public void SetOtherTransactionName_SetsOtherTransactionName()
		{
			const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
			SetupTransaction();

			_agent.CurrentTransaction.SetOtherTransactionName("cat", "foo", priority);

			var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
			Assert.AreEqual("cat/foo", addedTransactionName.UnprefixedName);
			Assert.AreEqual(false, addedTransactionName.IsWeb);
		}

		[Test]
		public void SetCustomTransactionName_SetsCustomTransactionName()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetOtherTransactionName("bar", "foo", TransactionNamePriority.Uri);
			_agent.CurrentTransaction.SetCustomTransactionName("foo", TransactionNamePriority.StatusCode);

			var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
			Assert.AreEqual("Custom/foo", addedTransactionName.UnprefixedName);
			Assert.AreEqual(false, addedTransactionName.IsWeb);
		}

		[Test]
		public void SetTransactionUri_SetsTransactionUri()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetUri("foo");

			Assert.AreEqual("foo", _transaction.TransactionMetadata.Uri);
		}

		[Test]
		public void SetTransactionOriginalUri_SetsTransactionOriginalUri()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetOriginalUri("foo");

			Assert.AreEqual("foo", _transaction.TransactionMetadata.OriginalUri);
		}

		[Test]
		public void SetTransactionReferrerUri_SetsTransactionReferrerUri()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetReferrerUri("foo");

			Assert.AreEqual("foo", _transaction.TransactionMetadata.ReferrerUri);
		}

		[Test]
		public void SetTransactionQueueTime_SetsTransactionQueueTime()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetQueueTime(TimeSpan.FromSeconds(4));

			Assert.AreEqual(TimeSpan.FromSeconds(4), _transaction.TransactionMetadata.QueueTime);
		}

		[Test]
		public void SetTransactionRequestParameters_SetsTransactionRequestParameters_ForRequestBucket()
		{
			SetupTransaction();

			var parameters = new Dictionary<string, string> { { "key", "value" } };

			_agent.CurrentTransaction.SetRequestParameters(parameters);

			Assert.AreEqual(1, _transaction.TransactionMetadata.RequestParameters.Length);
			Assert.AreEqual("key", _transaction.TransactionMetadata.RequestParameters[0].Key);
			Assert.AreEqual("value", _transaction.TransactionMetadata.RequestParameters[0].Value);
		}

		[Test]
		public void SetTransactionRequestParameters_SetMultipleRequestParameters()
		{
			SetupTransaction();

			var parameters = new Dictionary<string, string> { { "firstName", "Jane" }, { "lastName", "Doe" } };

			_agent.CurrentTransaction.SetRequestParameters(parameters);

			var result = _transaction.TransactionMetadata.RequestParameters.ToDictionary();
			Assert.AreEqual(2, _transaction.TransactionMetadata.RequestParameters.Length);
			Assert.Contains("firstName", result.Keys);
			Assert.Contains("lastName", result.Keys);
			Assert.Contains("Jane", result.Values);
			Assert.Contains("Doe", result.Values);
		}

		[Test]
		public void SetTransactionHttpResponseStatusCode_SetsTransactionHttpResponseStatusCode()
		{
			SetupTransaction();

			_agent.CurrentTransaction.SetHttpResponseStatusCode(1, 2);

			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			Assert.AreEqual(1, immutableTransactionMetadata.HttpResponseStatusCode);
			Assert.AreEqual(2, immutableTransactionMetadata.HttpResponseSubStatusCode);
		}

		#endregion Transaction metadata

		#region Segments

		[Test]
		public void StartTransactionSegment_ReturnsAnOpaqueSimpleSegmentBuilder()
		{
			SetupTransaction();

			var expectedParentId = 1;
			Mock.Arrange(() => _callStackManager.TryPeek()).Returns(expectedParentId);

			var invocationTarget = new Object();
			var method = new Method(typeof(string), "methodName", "parameterTypeNames");
			var methodCall = new MethodCall(method, invocationTarget, new Object[0]);
			var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(methodCall, "foo");
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
			SetupTransaction();

			var pushedUniqueId = (Object)null;
			Mock.Arrange(() => _callStackManager.Push(Arg.IsAny<int>()))
				.DoInstead<Object>(pushed => pushedUniqueId = pushed);

			var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), "foo");
			Assert.NotNull(opaqueSegment);

			var segment = opaqueSegment as TypedSegment<SimpleSegmentData>;
			Assert.NotNull(segment);

			var immutableSegment = segment as TypedSegment<SimpleSegmentData>;
			Assert.AreEqual(pushedUniqueId, immutableSegment.UniqueId);
		}

		[Test]
		public void StartExternalSegment_Throws_IfUriIsNotAbsolute()
		{
			SetupTransaction();
			var uri = new Uri("/test", UriKind.Relative);
			NrAssert.Throws<ArgumentException>(() => _agent.CurrentTransaction.StartExternalRequestSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), uri, "GET"));
		}

		#endregion Segments

		#region EndSegment

		[Test]
		public void EndSegment_RemovesSegmentFromCallStack()
		{
			SetupTransaction();

			var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new Object[0]), "foo");
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
			SetupTransaction();

			var now = DateTime.UtcNow;
			var exception = NotNewRelic.ExceptionBuilder.BuildException("My message");

			_agent.CurrentTransaction.NoticeError(exception);

			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			var errorData = immutableTransactionMetadata.TransactionExceptionDatas.First();

			NrAssert.Multiple(
				() => Assert.AreEqual("My message", errorData.ErrorMessage),
				() => Assert.AreEqual("System.Exception", errorData.ErrorTypeName),
				() => Assert.IsTrue(errorData.StackTrace?.Contains("NotNewRelic.ExceptionBuilder.BuildException") == true),
				() => Assert.IsTrue(errorData.NoticedAt >= now && errorData.NoticedAt < now.AddMinutes(1))
			);
		}

		[Test]
		public void NoticeError_SendsAllErrorDataToTransactionBuilder()
		{
			SetupTransaction();

			var exception = NotNewRelic.ExceptionBuilder.BuildException("My message");

			_agent.CurrentTransaction.NoticeError(exception);
			_agent.CurrentTransaction.NoticeError(exception);

			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			Assert.AreEqual(2, immutableTransactionMetadata.TransactionExceptionDatas.Count());
		}

		#endregion NoticeError

		#region inbound CAT request - outbound CAT response

		[Test]
		public void ProcessInboundRequest_SetsReferrerCrossProcessId()
		{
			var transactionName = TransactionName.ForWebTransaction("a", "b");
			Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
			var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
				.Returns(catRequestData);
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
				.Returns("referrerProcessId");
			Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
			var headers = new Dictionary<string, string>();

			_agent.ProcessInboundRequest(headers, TransportType.HTTP);

			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("referrerProcessId"));
		}

		[Test]
		public void ProcessInboundRequest_SetsTransactionData()
		{
			var transactionName = TransactionName.ForWebTransaction("a", "b");
			Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
			var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
				.Returns(catRequestData);
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
				.Returns("referrerProcessId");
			Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
			var headers = new Dictionary<string, string>();

			_agent.ProcessInboundRequest(headers, TransportType.HTTP);

			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId"));
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash"));
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referrerTransactionGuid"));
		}

		[Test]
		public void ProcessInboundRequest_SetsTransactionData_WithContentLength()
		{
			var transactionName = TransactionName.ForWebTransaction("a", "b");
			Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
			var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<IDictionary<string, string>>()))
				.Returns(catRequestData);
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<IDictionary<string, string>>()))
				.Returns("referrerProcessId");
			Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
			var headers = new Dictionary<string, string>();

			_agent.ProcessInboundRequest(headers, TransportType.HTTP, 200);

			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTripId("referrerTripId"));
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(200));
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash"));
			Mock.Assert(() => _transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid("referrerTransactionGuid"));
		}

		[Test]
		public void ProcessInboundRequest_DoesNotSetTransactionDataIfCrossProcessIdAlreadyExists()
		{
			var transactionName = TransactionName.ForWebTransaction("a", "b");
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

			_agent.ProcessInboundRequest(headers, TransportType.HTTP);

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
			_agent.ProcessInboundRequest(headers, TransportType.Unknown, null);

			// Simply not throwing is all this test needs to check for
		}

		[Test]
		public void ProcessInboundRequest__ReportsSupportabilityMetric_NullPayload()
		{
			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			
			var headers = new Dictionary<string, string>();
			headers[DistributedTraceHeaderName] = null;
			_agent.ProcessInboundRequest(headers, TransportType.HTTPS);

			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull(), Occurs.Once());
		}


		[Test]
		public void GetResponseMetadata_ReturnsEmpty_IfNoCurrentTransaction()
		{
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((IInternalTransaction) null);

			var headers = _agent.CurrentTransaction.GetResponseMetadata();

			Assert.NotNull(headers);
			Assert.IsEmpty(headers);
		}

		[Test]
		public void GetResponseMetadata_ReturnsEmpty_IfNoReferrerCrossProcessId()
		{
			var transactionName = TransactionName.ForWebTransaction("a", "b");
			Mock.Arrange(() => _transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
			Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
			Mock.Arrange(() => _transaction.TransactionMetadata.CrossApplicationReferrerProcessId).Returns(null as string);
			Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

			var headers = _agent.CurrentTransaction.GetResponseMetadata();

			Assert.NotNull(headers);
			Assert.IsEmpty(headers);
		}

		[Test]
		public void GetResponseMetadata_CallsSetPathHashWithResultsFromPathHashMaker()
		{
			SetupTransaction();

			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(new TransactionMetricName("c", "d"));
			Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");
			_transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash");
			_transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("foo");

			_agent.CurrentTransaction.GetResponseMetadata();

			Assert.AreEqual("pathHash", _transaction.TransactionMetadata.LatestCrossApplicationPathHash);
		}

		[Test]
		public void GetResponseMetadata_ReturnsCatHeadersFromCatHeaderHandler()
		{
			SetupTransaction();

			var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundResponseHeaders(Arg.IsAny<IInternalTransaction>(), Arg.IsAny<TransactionMetricName>())).Returns(catHeaders);

			_transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("CatReferrer");
			var headers = _agent.CurrentTransaction.GetResponseMetadata().ToDictionary();

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
			SetupTransaction();

			var expectedCatResponseData = new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3);
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundResponseHeaders(Arg.IsAny<IDictionary<string, string>>()))
				.Returns(expectedCatResponseData);

			var headers = new Dictionary<string, string>();
			var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));
			_agent.CurrentTransaction.ProcessInboundResponse(headers, segment);

			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			Assert.AreEqual(expectedCatResponseData, segment.TypedData.CrossApplicationResponseData);
			Assert.AreEqual(true, immutableTransactionMetadata.HasCatResponseHeaders);
		}

		[Test]
		public void ProcessInboundResponse_DoesNotThrow_WhenNoCurrentTransaction()
		{
			Transaction transaction = null;
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(transaction);

			var headers = new Dictionary<string, string>();
			var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));

			_agent.CurrentTransaction.ProcessInboundResponse(headers, segment);

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
			_agent.CurrentTransaction.ProcessInboundResponse(headers, segmentBuilder);

			var builtSegment = segmentBuilder as TypedSegment<ExternalSegmentData>;
			Assert.AreEqual(builtSegment.TypedData.CrossApplicationResponseData, expectedCatResponseData);
			Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());
		}

		[Test]
		public void ProcessInboundResponse_SetsSegmentCatResponseData()
		{
			SetupTransaction();

			var expectedCatResponseData = new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3);
			Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundResponseHeaders(Arg.IsAny<IDictionary<string, string>>()))
				.Returns(expectedCatResponseData);

			var headers = new Dictionary<string, string>();
			var segment = new TypedSegment<ExternalSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("foo", "bar", 1), new ExternalSegmentData(new Uri("http://www.google.com"), "method"));
			_agent.CurrentTransaction.ProcessInboundResponse(headers, segment);

			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			Assert.AreEqual(expectedCatResponseData, segment.TypedData.CrossApplicationResponseData);
			Assert.AreEqual(true, immutableTransactionMetadata.HasCatResponseHeaders);
		}

		[Test]
		public void ProcessInboundResponse_DoesNotThrow_IfSegmentIsNull()
		{
			var headers = new Dictionary<string, string>();
			_agent.CurrentTransaction.ProcessInboundResponse(headers, null);
			Mock.Assert(() => _transaction.TransactionMetadata.MarkHasCatResponseHeaders(), Occurs.Never());

			// Simply not throwing is all this test needs to check for
		}

		[Test]
		public void GetRequestMetadata_ReturnsEmpty_IfNoCurrentTransaction()
		{
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((IInternalTransaction) null);

			var headers = _agent.CurrentTransaction.GetRequestMetadata();

			Assert.NotNull(headers);
			Assert.IsEmpty(headers);
		}

		[Test]
		public void GetRequestMetadata_CallsSetPathHashWithResultsFromPathHashMaker()
		{
			SetupTransaction();

			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(new TransactionMetricName("c", "d"));
			Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");
			_transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash");

			_agent.CurrentTransaction.GetRequestMetadata();

			Assert.AreEqual("pathHash", _transaction.TransactionMetadata.LatestCrossApplicationPathHash);
		}

		[Test]
		public void GetRequestMetadata_ReturnsCatHeadersFromCatHeaderHandler()
		{
			SetupTransaction();

			var catHeaders = new Dictionary<string, string> {{"key1", "value1"}, {"key2", "value2"}};
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<IInternalTransaction>())).Returns(catHeaders);

			var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

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
			Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<IInternalTransaction>())).Returns(catHeaders);

			// Act
			var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

			// Assert
			const string NewRelicIdHttpHeader = "X-NewRelic-ID";
			const string TransactionDataHttpHeader = "X-NewRelic-Transaction";
			const string AppDataHttpHeader = "X-NewRelic-App-Data";

			Assert.That(headers, Does.Not.ContainKey(NewRelicIdHttpHeader));
			Assert.That(headers, Does.Not.ContainKey(TransactionDataHttpHeader));
			Assert.That(headers, Does.Not.ContainKey(AppDataHttpHeader));

			Mock.Assert(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<IInternalTransaction>()), Occurs.Never());
		}

		[Test]
		public void GetRequestMetadata_ReturnsDistributedTraceHeadersFromDTPayloadHandler_IfDistributedTraceIsEnabled()
		{
			// Arrange
			SetupTransaction();
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var distributedTraceHeaders = Mock.Create<IDistributedTracePayload>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<IInternalTransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			// Act
			var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

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
			SetupTransaction();
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
			var headers = new Dictionary<string, string>() { { DistributedTraceHeaderName, "encodedpayload" } };

			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			// Act
			_agent.ProcessInboundRequest(headers, TransportType.HTTP);

			// Assert
			var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerProcessId);
			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerTripId);
			Assert.AreEqual(-1, _transaction.TransactionMetadata.GetCrossApplicationReferrerContentLength());
			Assert.IsNull(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash);
			Assert.IsNull(null, immutableTransactionMetadata.CrossApplicationReferrerTransactionGuid);

			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceAccountId, _accountId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceAppId, _appId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceGuid, _guid);
			Assert.AreEqual(_transaction.TransactionMetadata.Priority, _priority);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceTraceId, _traceId);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceTrustKey, _trustKey);
			Assert.AreEqual(_transaction.TransactionMetadata.DistributedTraceType, _type);
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
			_agent.ProcessInboundRequest(new KeyValuePair<string, string>[] { }, TransportType.HTTP);

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
			_agent.ProcessInboundRequest(new KeyValuePair<string, string>[] { }, TransportType.HTTP);

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
			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var payload = string.Empty;
			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);
			_agent.CurrentTransaction.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).CallOriginal();
			_agent.CurrentTransaction.AcceptDistributedTracePayload(payload, TransportType.HTTPS);


			Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple(), Occurs.Once());
		}

		[Test]
		public void CreateDistributedTracePayload_ShouldReturnPayload_IfConfigIsTrue()
		{
			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var distributedTraceHeaders = Mock.Create<IDistributedTracePayload>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<IInternalTransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			var payload = _agent.CurrentTransaction.CreateDistributedTracePayload();

			NrAssert.Multiple(
				() => Assert.AreEqual(distributedTraceHeaders, payload)
			);
		}

		[Test]
		public void CreateDistributedTracePayload_ShouldNotReturnPayload_IfConfigIsFalse()
		{
			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

			var distributedTraceHeaders = Mock.Create<IDistributedTracePayload>();
			Mock.Arrange(() => distributedTraceHeaders.HttpSafe()).Returns("value1");

			Mock.Arrange(() => _distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(Arg.IsAny<IInternalTransaction>(), Arg.IsAny<ISegment>())).Returns(distributedTraceHeaders);

			var payload = _agent.CurrentTransaction.CreateDistributedTracePayload();

			Assert.AreEqual(DistributedTraceApiModel.EmptyModel, payload);
		}

		[Test]
		public void TraceMetadata_ShouldReturnEmptyModel_IfDTConfigIsFalse()
		{
			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

			var traceMetadata = _agent.TraceMetadata;

			Assert.AreEqual(TraceMetadata.EmptyModel, traceMetadata);
		}

		[Test]
		public void TraceMetadata_ShouldReturnValidValues_IfDTConfigIsTrue()
		{
			const string testTraceId = "testTraceId";
			const string testSpanId = "testSpanId";
			const bool testIsSampled = true;

			SetupTransaction();

			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var traceMetadata = Mock.Create<ITraceMetadata>();
			Mock.Arrange(() => traceMetadata.TraceId).Returns(testTraceId);
			Mock.Arrange(() => traceMetadata.SpanId).Returns(testSpanId);
			Mock.Arrange(() => traceMetadata.IsSampled).Returns(testIsSampled);

			Assert.AreEqual(traceMetadata.TraceId, testTraceId);
			Assert.AreEqual(traceMetadata.SpanId, testSpanId);
			Assert.AreEqual(traceMetadata.IsSampled, testIsSampled);
		}

		private void SetupTransaction()
		{
			var transactionName = TransactionName.ForWebTransaction("foo", "bar");
			_transaction = new Transaction(_configurationService.Configuration, transactionName, Mock.Create<ITimer>(), DateTime.UtcNow, _callStackManager, SqlObfuscator.GetObfuscatingSqlObfuscator(), default(float), Mock.Create<IDatabaseStatementParser>());

			//_transactionService = Mock.Create<ITransactionService>();
			Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(_transaction);
		}

		[Test]
		public void AcceptDistributedTracePayload_ShouldAccept_IfConfigIsTrue()
		{
			SetupTransaction();
			Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

			var payload = string.Empty;
			Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

			_agent.CurrentTransaction.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

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

			_agent.CurrentTransaction.AcceptDistributedTracePayload(payload, TransportType.HTTPS);

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

			var actualResult = _agent.TryGetDistributedTracePayloadFromHeaders(headers, out var actualPayload);

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
			var actualResult = _agent.TryGetDistributedTracePayloadFromHeaders(headers, out var actualPayload);

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