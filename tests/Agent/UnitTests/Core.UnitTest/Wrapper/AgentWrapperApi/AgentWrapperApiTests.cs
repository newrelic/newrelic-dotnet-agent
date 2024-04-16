// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Collections;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
    [TestFixture]
    public class AgentWrapperApiTests
    {
        private IInternalTransaction _transaction;

        private ITransactionService _transactionService;

        private IAgent _agent;

        private ITransactionTransformer _transactionTransformer;

        private ICallStackManager _callStackManager;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;

        private IPathHashMaker _pathHashMaker;

        private ICatHeaderHandler _catHeaderHandler;

        private IDistributedTracePayloadHandler _distributedTracePayloadHandler;

        private ISyntheticsHeaderHandler _syntheticsHeaderHandler;

        private ITransactionFinalizer _transactionFinalizer;

        private IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;

        private IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

        private IConfigurationService _configurationService;

        private IAgentHealthReporter _agentHealthReporter;
        private ISimpleSchedulingService _simpleSchedulingService;
        private ITraceMetadataFactory _traceMetadataFactory;
        private ICATSupportabilityMetricCounters _catMetrics;
        private IThreadPoolStatic _threadPoolStatic;
        private IAgentTimerService _agentTimerService;
        private IMetricNameService _metricNameService;
        private IErrorService _errorService;
        private ILogEventAggregator _logEventAggregator;
        private ILogContextDataFilter _logContextDataFilter;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        private Action _harvestAction;
        private TimeSpan? _harvestCycle;

        private const string DistributedTraceHeaderName = "newrelic";
        private const string ReferrerTripId = "referrerTripId";
        private const string ReferrerPathHash = "referrerPathHash";
        private const string ReferrerTransactionGuid = "referrerTransactionGuid";
        private const string ReferrerProcessId = "referrerProcessId";

        private ICustomEventTransformer _customEventTransformer;

        [SetUp]
        public void SetUp()
        {
            _callStackManager = Mock.Create<ICallStackManager>();
            _transaction = Mock.Create<IInternalTransaction>();
            var transactionSegmentState = TransactionSegmentStateHelpers.GetItransactionSegmentState();
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

            _threadPoolStatic = Mock.Create<IThreadPoolStatic>();
            Mock.Arrange(() => _threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>()))
                .DoInstead<WaitCallback>(callback => callback(null));
            Mock.Arrange(() => _threadPoolStatic.QueueUserWorkItem(Arg.IsAny<WaitCallback>(), Arg.IsAny<object>()))
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
            _agentTimerService = Mock.Create<IAgentTimerService>();
            _agentTimerService = Mock.Create<IAgentTimerService>();
            _metricNameService = new MetricNameService();
            _catMetrics = Mock.Create<ICATSupportabilityMetricCounters>();
            _simpleSchedulingService = Mock.Create<ISimpleSchedulingService>();
            _distributedTracePayloadHandler = Mock.Create<IDistributedTracePayloadHandler>();
            _traceMetadataFactory = Mock.Create<ITraceMetadataFactory>();
            _errorService = new ErrorService(_configurationService);
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, harvestCycle, __) => { _harvestAction = action; _harvestCycle = harvestCycle; });
            _logEventAggregator = new LogEventAggregator(Mock.Create<IDataTransportService>(), scheduler, Mock.Create<IProcessStatic>(), _agentHealthReporter);
            _logContextDataFilter = new LogContextDataFilter(_configurationService);

            _customEventTransformer = Mock.Create<ICustomEventTransformer>();

            _agent = new Agent(_transactionService, _transactionTransformer, _threadPoolStatic, _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, _distributedTracePayloadHandler, _syntheticsHeaderHandler, _transactionFinalizer, _browserMonitoringPrereqChecker, _browserMonitoringScriptMaker, _configurationService, _agentHealthReporter, _agentTimerService, _metricNameService, _traceMetadataFactory, _catMetrics, _logEventAggregator, _logContextDataFilter, _simpleSchedulingService, _customEventTransformer);
        }

        [TearDown]
        public void TearDown()
        {
            _metricNameService.Dispose();
            _logEventAggregator.Dispose();
            _logContextDataFilter.Dispose();
            _attribDefSvc.Dispose();
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

                var foundResponseTimeAlreadyCapturedMessage = logging.HasMessageBeginningWith("Transaction has already captured the response time.");
                Assert.That(foundResponseTimeAlreadyCapturedMessage, Is.False);
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
                Assert.That(foundResponseTimeAlreadyCapturedMessage, Is.True);
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
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.Category, Is.EqualTo("MVC"));
                Assert.That(addedTransactionName.Name, Is.EqualTo("foo"));
            });
        }

        [Test]
        public void SetWebTransactionNameFromPath_SetsUriTransactionName()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetWebTransactionNameFromPath(WebTransactionType.MVC, "some/path");

            var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.UnprefixedName, Is.EqualTo("Uri/some/path"));
                Assert.That(addedTransactionName.IsWeb, Is.EqualTo(true));
            });
        }

        [Test]
        public void SetMessageBrokerTransactionName_SetsMessageBrokerTransactionName()
        {
            const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
            SetupTransaction();

            _agent.CurrentTransaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "broker", "dest", priority);

            var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.UnprefixedName, Is.EqualTo("Message/broker/Topic/Named/dest"));
                Assert.That(addedTransactionName.IsWeb, Is.EqualTo(false));
            });
        }

        [Test]
        public void SetKafkaMessageBrokerTransactionName_SetsKafkaMessageBrokerTransactionName()
        {
            const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
            SetupTransaction();

            _agent.CurrentTransaction.SetKafkaMessageBrokerTransactionName(MessageBrokerDestinationType.Topic, "broker", "dest", priority);

            var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.UnprefixedName, Is.EqualTo("Message/broker/Topic/Consume/Named/dest"));
                Assert.That(addedTransactionName.IsWeb, Is.EqualTo(false));
            });
        }

        [Test]
        public void SetOtherTransactionName_SetsOtherTransactionName()
        {
            const TransactionNamePriority priority = TransactionNamePriority.FrameworkHigh;
            SetupTransaction();

            _agent.CurrentTransaction.SetOtherTransactionName("cat", "foo", priority);

            var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.UnprefixedName, Is.EqualTo("cat/foo"));
                Assert.That(addedTransactionName.IsWeb, Is.EqualTo(false));
            });
        }

        [Test]
        public void SetCustomTransactionName_SetsCustomTransactionName()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetOtherTransactionName("bar", "foo", TransactionNamePriority.Uri);
            _agent.CurrentTransaction.SetCustomTransactionName("foo", TransactionNamePriority.StatusCode);

            var addedTransactionName = _transaction.CandidateTransactionName.CurrentTransactionName;
            Assert.Multiple(() =>
            {
                Assert.That(addedTransactionName.UnprefixedName, Is.EqualTo("Custom/foo"));
                Assert.That(addedTransactionName.IsWeb, Is.EqualTo(false));
            });
        }

        [Test]
        public void SetTransactionUri_SetsTransactionUri()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetUri("foo");

            Assert.That(_transaction.TransactionMetadata.Uri, Is.EqualTo("foo"));
        }

        [Test]
        public void SetTransactionOriginalUri_SetsTransactionOriginalUri()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetOriginalUri("foo");

            Assert.That(_transaction.TransactionMetadata.OriginalUri, Is.EqualTo("foo"));
        }

        [Test]
        public void SetTransactionReferrerUri_SetsTransactionReferrerUri()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetReferrerUri("foo");

            Assert.That(_transaction.TransactionMetadata.ReferrerUri, Is.EqualTo("foo"));
        }

        [Test]
        public void SetTransactionQueueTime_SetsTransactionQueueTime()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetQueueTime(TimeSpan.FromSeconds(4));

            Assert.That(_transaction.TransactionMetadata.QueueTime, Is.EqualTo(TimeSpan.FromSeconds(4)));
        }

        [Test]
        public void SetTransactionRequestParameters_SetsTransactionRequestParameters_ForRequestBucket()
        {
            SetupTransaction();

            var parameters = new Dictionary<string, string> { { "key", "value" } };

            _agent.CurrentTransaction.SetRequestParameters(parameters);

            var attribValDic = _transaction.TransactionMetadata.UserAndRequestAttributes.ToDictionary();

            Assert.That(attribValDic, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(attribValDic.First().Key, Is.EqualTo("request.parameters.key"));
                Assert.That(attribValDic.First().Value, Is.EqualTo("value"));
            });
        }

        [Test]
        public void SetTransactionRequestParameters_SetMultipleRequestParameters()
        {
            SetupTransaction();

            var parameters = new Dictionary<string, string> { { "firstName", "Jane" }, { "lastName", "Doe" } };

            _agent.CurrentTransaction.SetRequestParameters(parameters);

            var result = _transaction.TransactionMetadata.UserAndRequestAttributes.ToDictionary();
            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(2));
                Assert.That(result.Keys, Does.Contain("request.parameters.firstName"));
                Assert.That(result.Keys, Does.Contain("request.parameters.lastName"));
                Assert.That(result.Values, Does.Contain("Jane"));
                Assert.That(result.Values, Does.Contain("Doe"));
            });
        }

        [Test]
        public void SetTransactionHttpResponseStatusCode_SetsTransactionHttpResponseStatusCode()
        {
            SetupTransaction();

            _agent.CurrentTransaction.SetHttpResponseStatusCode(1, 2);

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(immutableTransactionMetadata.HttpResponseStatusCode, Is.EqualTo(1));
                Assert.That(immutableTransactionMetadata.HttpResponseSubStatusCode, Is.EqualTo(2));
            });
        }

        #endregion Transaction metadata

        #region Segments

        [Test]
        public void StartTransactionSegment_ReturnsAnOpaqueSimpleSegmentBuilder()
        {
            SetupTransaction();

            var expectedParentId = 1;
            Mock.Arrange(() => _callStackManager.TryPeek()).Returns(expectedParentId);

            var invocationTarget = new object();
            var method = new Method(typeof(string), "methodName", "parameterTypeNames");
            var methodCall = new MethodCall(method, invocationTarget, new object[0], false);
            var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(methodCall, "foo");
            Assert.That(opaqueSegment, Is.Not.Null);

            var segment = opaqueSegment as Segment;
            Assert.That(segment, Is.Not.Null);

            var immutableSegment = segment as Segment;
            var simpleSegmentData = immutableSegment.Data as SimpleSegmentData;
            NrAssert.Multiple(
                () => Assert.That(immutableSegment.ParentUniqueId, Is.EqualTo(expectedParentId)),
                () => Assert.That(simpleSegmentData?.Name, Is.EqualTo("foo")),
                () => Assert.That(immutableSegment.MethodCallData.TypeName, Is.EqualTo("System.String")),
                () => Assert.That(immutableSegment.MethodCallData.MethodName, Is.EqualTo("methodName")),
                () => Assert.That(immutableSegment.MethodCallData.InvocationTargetHashCode, Is.EqualTo(RuntimeHelpers.GetHashCode(invocationTarget)))
            );
        }

        [Test]
        public void StartTransactionSegment_PushesNewSegmentUniqueIdToCallStack()
        {
            SetupTransaction();

            var pushedUniqueId = (object)null;
            Mock.Arrange(() => _callStackManager.Push(Arg.IsAny<int>()))
                .DoInstead<object>(pushed => pushedUniqueId = pushed);

            var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0], false), "foo");
            Assert.That(opaqueSegment, Is.Not.Null);

            var segment = opaqueSegment as Segment;
            Assert.That(segment, Is.Not.Null);

            var immutableSegment = segment as Segment;
            Assert.That(immutableSegment.UniqueId, Is.EqualTo(pushedUniqueId));
        }

        [Test]
        public void StartExternalSegment_Throws_IfUriIsNotAbsolute()
        {
            SetupTransaction();
            var uri = new Uri("/test", UriKind.Relative);
            NrAssert.Throws<ArgumentException>(() => _agent.CurrentTransaction.StartExternalRequestSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0], false), uri, "GET"));
        }

        #endregion Segments

        #region EndSegment

        [Test]
        public void EndSegment_RemovesSegmentFromCallStack()
        {
            SetupTransaction();

            var opaqueSegment = _agent.CurrentTransaction.StartTransactionSegment(new MethodCall(new Method(typeof(string), "", ""), "", new object[0], false), "foo");
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
            var errorData = immutableTransactionMetadata.ReadOnlyTransactionErrorState.ErrorData;

            NrAssert.Multiple(
                () => Assert.That(errorData.ErrorMessage, Is.EqualTo("My message")),
                () => Assert.That(errorData.ErrorTypeName, Is.EqualTo("System.Exception")),
                () => Assert.That(errorData.StackTrace?.Contains("NotNewRelic.ExceptionBuilder.BuildException"), Is.EqualTo(true)),
                () => Assert.That(errorData.NoticedAt >= now && errorData.NoticedAt < now.AddMinutes(1), Is.True)
            );
        }

        #endregion NoticeError

        #region inbound CAT request - outbound CAT response

        [Test]
        public void AcceptDistributedTraceHeaders_SetsReferrerCrossProcessId()
        {
            SetupTransaction();

            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(ReferrerProcessId);
            var headers = new Dictionary<string, string>();

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerProcessId, Is.EqualTo(ReferrerProcessId), $"CrossApplicationReferrerProcessId");
        }

        [Test]
        public void AcceptDistributedTraceHeaders_SetsTransactionData()
        {
            SetupTransaction();

            var catRequestData = new CrossApplicationRequestData(ReferrerTransactionGuid, false, ReferrerTripId, ReferrerPathHash);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(catRequestData);
            var headers = new Dictionary<string, string>();

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            Assert.Multiple(() =>
            {
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTripId, Is.EqualTo(ReferrerTripId), $"CrossApplicationReferrerTripId");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash, Is.EqualTo(ReferrerPathHash), $"CrossApplicationReferrerPathHash");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid, Is.EqualTo(ReferrerTransactionGuid), $"CrossApplicationReferrerTransactionGuid");
            });
        }

        [Test]
        public void AcceptDistributedTraceHeaders_SetsTransactionData_WithContentLength()
        {
            SetupTransaction();

            var catRequestData = new CrossApplicationRequestData("referrerTransactionGuid", false, "referrerTripId", "referrerPathHash");
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(catRequestData);
            var headers = new Dictionary<string, string>() { { "Content-Length", "200" } };
            var transactionExperimental = _agent.CurrentTransaction.GetExperimentalApi();

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            Assert.Multiple(() =>
            {
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTripId, Is.EqualTo(ReferrerTripId), $"CrossApplicationReferrerTripId");
                Assert.That(_transaction.TransactionMetadata.GetCrossApplicationReferrerContentLength(), Is.EqualTo(200), $"CrossApplicationContentLength");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash, Is.EqualTo(ReferrerPathHash), $"CrossApplicationReferrerPathHash");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid, Is.EqualTo(ReferrerTransactionGuid), $"CrossApplicationReferrerTransactionGuid");
            });
        }

        [Test]
        public void AcceptDistributedTraceHeaders_DoesNotSetTransactionDataIfCrossProcessIdAlreadyExists()
        {
            SetupTransaction();

            var newReferrerProcessId = "newProcessId";
            var newReferrerTransactionGuid = "newTxGuid";
            var newReferrerTripId = "newTripId";
            var newReferrerPathHash = "newPathHash";

            // first request to set TransactionMetadata
            var catRequestData = new CrossApplicationRequestData(ReferrerTransactionGuid, false, ReferrerTripId, ReferrerPathHash);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(ReferrerProcessId);

            var headers = new Dictionary<string, string>();

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            // new request
            catRequestData = new CrossApplicationRequestData(newReferrerTransactionGuid, false, newReferrerTripId, newReferrerPathHash);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(catRequestData);
            Mock.Arrange(() => _catHeaderHandler.TryDecodeInboundRequestHeadersForCrossProcessId(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>()))
                .Returns(newReferrerProcessId);

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            Assert.Multiple(() =>
            {
                // values are for first request
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerProcessId, Is.EqualTo(ReferrerProcessId), $"CrossApplicationReferrerProcessId");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTripId, Is.EqualTo(ReferrerTripId), $"CrossApplicationReferrerTripId");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash, Is.EqualTo(ReferrerPathHash), $"CrossApplicationReferrerPathHash");
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid, Is.EqualTo(ReferrerTransactionGuid), $"CrossApplicationReferrerTransactionGuid");
            });
        }

        [Test]
        public void AcceptDistributedTraceHeaders_DoesNotThrow_IfContentLengthIsNull()
        {
            var headers = new Dictionary<string, string>();
            Assert.DoesNotThrow(() => _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP));
        }

        [Test]
        public void AcceptDistributedTraceHeaders__ReportsSupportabilityMetric_NullPayload()
        {
            _distributedTracePayloadHandler = new DistributedTracePayloadHandler(_configurationService, _agentHealthReporter, new AdaptiveSampler());
            _agent = new Agent(_transactionService, _transactionTransformer, _threadPoolStatic, _transactionMetricNameMaker, _pathHashMaker, _catHeaderHandler, _distributedTracePayloadHandler, _syntheticsHeaderHandler, _transactionFinalizer, _browserMonitoringPrereqChecker, _browserMonitoringScriptMaker, _configurationService, _agentHealthReporter, _agentTimerService, _metricNameService, _traceMetadataFactory, _catMetrics, _logEventAggregator, _logContextDataFilter, _simpleSchedulingService, _customEventTransformer);
            SetupTransaction();

            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

            var headers = new Dictionary<string, string>();
            headers[DistributedTraceHeaderName] = null;

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull(), Occurs.Once());
        }

        [Test]
        public void GetResponseMetadata_ReturnsEmpty_IfNoCurrentTransaction()
        {
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((IInternalTransaction)null);

            var headers = _agent.CurrentTransaction.GetResponseMetadata();

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers, Is.Empty);
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

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers, Is.Empty);
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

            Assert.That(_transaction.TransactionMetadata.LatestCrossApplicationPathHash, Is.EqualTo("pathHash"));
        }

        [Test]
        public void GetResponseMetadata_ReturnsCatHeadersFromCatHeaderHandler()
        {
            SetupTransaction();

            var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            Mock.Arrange(() => _catHeaderHandler.TryGetOutboundResponseHeaders(Arg.IsAny<IInternalTransaction>(), Arg.IsAny<TransactionMetricName>())).Returns(catHeaders);

            _transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId("CatReferrer");
            var headers = _agent.CurrentTransaction.GetResponseMetadata().ToDictionary();

            Assert.That(headers, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(headers["key1"], Is.EqualTo("value1")),
                () => Assert.That(headers["key2"], Is.EqualTo("value2"))
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
            var externalSegmentData = new ExternalSegmentData(new Uri("http://www.google.com"), "method");
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(externalSegmentData);

            _agent.CurrentTransaction.ProcessInboundResponse(headers, segment);

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(externalSegmentData.CrossApplicationResponseData, Is.EqualTo(expectedCatResponseData));
                Assert.That(immutableTransactionMetadata.HasCatResponseHeaders, Is.EqualTo(true));
            });
        }

        [Test]
        public void ProcessInboundResponse_DoesNotThrow_WhenNoCurrentTransaction()
        {
            Transaction transaction = null;
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(transaction);

            var headers = new Dictionary<string, string>();
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(new ExternalSegmentData(new Uri("http://www.google.com"), "method"));

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
            var externalSegmentData = new ExternalSegmentData(new Uri("http://www.google.com"), "method");
            var segmentBuilder = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segmentBuilder.SetSegmentData(externalSegmentData);

            _agent.CurrentTransaction.ProcessInboundResponse(headers, segmentBuilder);

            Assert.That(expectedCatResponseData, Is.EqualTo(externalSegmentData.CrossApplicationResponseData));
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
            var externalSegmentData = new ExternalSegmentData(new Uri("http://www.google.com"), "method");
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("foo", "bar", 1));
            segment.SetSegmentData(externalSegmentData);

            _agent.CurrentTransaction.ProcessInboundResponse(headers, segment);

            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(externalSegmentData.CrossApplicationResponseData, Is.EqualTo(expectedCatResponseData));
                Assert.That(immutableTransactionMetadata.HasCatResponseHeaders, Is.EqualTo(true));
            });
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
            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns((IInternalTransaction)null);

            var headers = _agent.CurrentTransaction.GetRequestMetadata();

            Assert.That(headers, Is.Not.Null);
            Assert.That(headers, Is.Empty);
        }

        [Test]
        public void GetRequestMetadata_CallsSetPathHashWithResultsFromPathHashMaker()
        {
            SetupTransaction();

            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");
            _transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash("referrerPathHash");

            _agent.CurrentTransaction.GetRequestMetadata();

            Assert.That(_transaction.TransactionMetadata.LatestCrossApplicationPathHash, Is.EqualTo("pathHash"));
        }

        [Test]
        public void GetRequestMetadata_ReturnsCatHeadersFromCatHeaderHandler()
        {
            SetupTransaction();

            var catHeaders = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } };
            Mock.Arrange(() => _catHeaderHandler.TryGetOutboundRequestHeaders(Arg.IsAny<IInternalTransaction>())).Returns(catHeaders);

            var headers = _agent.CurrentTransaction.GetRequestMetadata().ToDictionary();

            Assert.That(headers, Is.Not.Null);
            NrAssert.Multiple(
                () => Assert.That(headers["key1"], Is.EqualTo("value1")),
                () => Assert.That(headers["key2"], Is.EqualTo("value2"))
            );
        }

        #endregion outbound CAT request - inbound CAT response

        #region Distributed Trace

        private static readonly string _accountId = "acctid";
        private static readonly string _appId = "appid";
        private static readonly string _guid = "guid";
        private static readonly float _priority = .3f;
        private static readonly bool _sampled = true;
        private static readonly string _traceId = "traceid";
        private static readonly string _trustKey = "trustedkey";
        private static readonly DistributedTracingParentType _type = DistributedTracingParentType.App;
        private static readonly string _transactionId = "transactionId";

        private readonly DistributedTracePayload _distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(_type.ToString(), _accountId, _appId, _guid, _traceId, _trustKey, _priority, _sampled, DateTime.UtcNow, _transactionId);

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
                () => Assert.That(headers[DistributedTraceHeaderName], Is.EqualTo(distributedTraceHeaders.HttpSafe()))
            );
        }

        [Test]
        public void AcceptDistributedTraceHeaders_SetsDTMetadataAndNotCATMetadata_IfDistributedTraceIsEnabled()
        {
            // Arrange
            SetupTransaction();
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            var headers = new Dictionary<string, string>() { { DistributedTraceHeaderName, "encodedpayload" } };

            Mock.Arrange(() => _distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(Arg.IsAny<string>())).Returns(_distributedTracePayload);

            var tracingState = Mock.Create<ITracingState>();
            Mock.Arrange(() => tracingState.Type).Returns(_type);
            Mock.Arrange(() => tracingState.AppId).Returns(_appId);
            Mock.Arrange(() => tracingState.AccountId).Returns(_accountId);
            Mock.Arrange(() => tracingState.Guid).Returns(_guid);
            Mock.Arrange(() => tracingState.TraceId).Returns(_traceId);
            Mock.Arrange(() => tracingState.TransactionId).Returns(_transactionId);
            Mock.Arrange(() => tracingState.Sampled).Returns(_sampled);
            Mock.Arrange(() => tracingState.Priority).Returns(_priority);

            Mock.Arrange(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(headers, Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>(), Arg.IsAny<DateTime>())).Returns(tracingState);

            // Act
            _transaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            // Assert
            var immutableTransactionMetadata = _transaction.TransactionMetadata.ConvertToImmutableMetadata();
            Assert.Multiple(() =>
            {
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerProcessId, Is.Null);
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerTripId, Is.Null);
                Assert.That(_transaction.TransactionMetadata.GetCrossApplicationReferrerContentLength(), Is.EqualTo(-1));
                Assert.That(_transaction.TransactionMetadata.CrossApplicationReferrerPathHash, Is.Null);
                Assert.That(immutableTransactionMetadata.CrossApplicationReferrerTransactionGuid, Is.Null);

                Assert.That(_transaction.TracingState.AccountId, Is.EqualTo(_accountId));
                Assert.That(_transaction.TracingState.AppId, Is.EqualTo(_appId));
                Assert.That(_transaction.TracingState.Guid, Is.EqualTo(_guid));
                Assert.That(_transaction.TracingState.Type, Is.EqualTo(_type));
                Assert.That(_transaction.TracingState.TransactionId, Is.EqualTo(_transactionId));
            });
        }

        [Test]
        public void AcceptDistributedTraceHeaders_DoesNotSetTransactionData_IfPayloadAlreadyAccepted()
        {
            // Arrange
            SetupTransaction();

            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);

            var tracingState = Mock.Create<ITracingState>();

            Mock.Arrange(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>(), Arg.IsAny<DateTime>())).Returns(tracingState);

            var headers = new Dictionary<string, string>();
            _transaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            // Act
            _transaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            // Assert
            Mock.Assert(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>(), Arg.IsAny<DateTime>()), Occurs.Once());
            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple(), Occurs.Once());
        }

        [Test]
        public void AcceptDistributedTraceHeaders_DoesNotSetTransactionData_IfOutgoingPayloadCreated()
        {
            // Arrange
            SetupTransaction();

            _transaction.TransactionMetadata.HasOutgoingTraceHeaders = true;

            var headers = new Dictionary<string, string>();

            // Act
            _transaction.AcceptDistributedTraceHeaders(headers, HeaderFunctions.GetHeaders, TransportType.HTTP);

            // Assert
            Mock.Assert(() => _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(Arg.IsAny<Dictionary<string, string>>(), Arg.IsAny<Func<Dictionary<string, string>, string, IEnumerable<string>>>(), Arg.IsAny<TransportType>(), Arg.IsAny<DateTime>()), Occurs.Never());
            Mock.Assert(() => _transaction.TracingState.AccountId, Occurs.Never());
            Mock.Assert(() => _transaction.TracingState.AppId, Occurs.Never());
            Mock.Assert(() => _transaction.TracingState.Guid, Occurs.Never());
            Mock.Assert(() => _transaction.TraceId, Occurs.Never());
            Mock.Assert(() => _transaction.TracingState.Type, Occurs.Never());
        }

        [Test]
        public void TraceMetadata_ShouldReturnEmptyModel_IfDTConfigIsFalse()
        {
            SetupTransaction();

            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

            var traceMetadata = _agent.TraceMetadata;

            Assert.That(traceMetadata, Is.EqualTo(TraceMetadata.EmptyModel));
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

            Assert.Multiple(() =>
            {
                Assert.That(traceMetadata.TraceId, Is.EqualTo(testTraceId));
                Assert.That(traceMetadata.SpanId, Is.EqualTo(testSpanId));
                Assert.That(traceMetadata.IsSampled, Is.EqualTo(testIsSampled));
            });
        }

        #endregion Distributed Trace

        #region TraceMetadata

        [Test]
        public void TraceMetadata_ReturnsEmptyTraceMetadata_WhenDistributedTracingDisabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);

            var actualTraceMetadata = _agent.TraceMetadata;

            Assert.That(actualTraceMetadata, Is.EqualTo(TraceMetadata.EmptyModel));
        }

        [Test]
        public void TraceMetadata_ReturnsEmptyTraceMetadata_WhenTransactionNotAvailable()
        {
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);
            Mock.Arrange(() => _transaction.IsValid).Returns(false);

            var actualTraceMetadata = _agent.TraceMetadata;

            Assert.That(actualTraceMetadata, Is.EqualTo(TraceMetadata.EmptyModel));
        }

        [Test]
        public void TraceMetadata_ReturnsTraceMetadata_DTAndTransactionAreAvailable()
        {
            var expectedTraceMetadata = new TraceMetadata("traceId", "spanId", true);
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(expectedTraceMetadata);

            var actualTraceMetadata = _agent.TraceMetadata;

            Assert.That(actualTraceMetadata, Is.EqualTo(expectedTraceMetadata));
        }

        #endregion TraceMetadata

        #region GetLinkingMetadata

        [Test]
        public void GetLinkingMetadata_ReturnsAllData_WhenAllDataIsAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            var expectedLinkingMetadata = new Dictionary<string, string>
            {
                { "trace.id", "traceId" },
                { "span.id", "spanId" },
                { "entity.name", "AppName1" },
                { "entity.type", "SERVICE" },
                { "entity.guid", "EntityGuid" },
                { "hostname", "FullHostName" }
            };

            Assert.That(actualLinkingMetadata, Is.EquivalentTo(expectedLinkingMetadata));
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeTraceId_WhenTraceIdIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata(string.Empty, "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("trace.id"), Is.False);
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeSpanId_WhenSpanIdIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", string.Empty, true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("span.id"), Is.False);
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeSpanIdOrTraceId_WhenTraceMetadataIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(false);
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.Keys, Is.Not.SupersetOf(new[] { "trace.id", "span.id" }));
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeEntityName_WhenFirstAppNameIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { string.Empty, "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("entity.name"), Is.False);
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeEntityName_WhenAppNameIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new string[0]);
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("entity.name"), Is.False);
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeEntityGuid_WhenEntityGuidIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns("FullHostName");
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns(string.Empty);

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("entity.guid"), Is.False);
        }

        [Test]
        public void GetLinkingMetadata_IncludesHostName_WhenFullHostNameIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns(string.Empty);
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns("HostName");
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata["hostname"], Is.EqualTo("HostName"));
        }

        [Test]
        public void GetLinkingMetadata_DoesNotIncludeHostName_WhenHostNameIsNotAvailable()
        {
            //TraceMetadata
            Mock.Arrange(() => _configurationService.Configuration.DistributedTracingEnabled).Returns(true);
            Mock.Arrange(() => _transaction.IsValid).Returns(true);
            Mock.Arrange(() => _traceMetadataFactory.CreateTraceMetadata(_transaction)).Returns(new TraceMetadata("traceId", "spanId", true));
            //HostName
            Mock.Arrange(() => _configurationService.Configuration.UtilizationFullHostName).Returns(string.Empty);
            Mock.Arrange(() => _configurationService.Configuration.UtilizationHostName).Returns(string.Empty);
            //ApplicationName
            Mock.Arrange(() => _configurationService.Configuration.ApplicationNames).Returns(new[] { "AppName1", "AppName2", "AppName3" });
            //EntityGuid
            Mock.Arrange(() => _configurationService.Configuration.EntityGuid).Returns("EntityGuid");

            var actualLinkingMetadata = _agent.GetLinkingMetadata();

            Assert.That(actualLinkingMetadata.ContainsKey("hostname"), Is.False);
        }

        #endregion GetLinkingMetadata

        #region Log Events

        [Test]
        public void RecordLogMessage_LogEventsDisabled_DropsEvent()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(false);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => null;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Is.Empty);
                Assert.That(logEvent, Is.Null);
            });
        }

        [Test]
        public void RecordLogMessage_NoTransaction_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.LogMetricsCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
            });

            Mock.Assert(() => _agentHealthReporter.IncrementLogLinesCount(Arg.AnyString), Occurs.Once());
        }

        [Test]
        public void RecordLogMessage_NoTransaction_NoMessage_WithException_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
            });
        }

        [Test]
        public void RecordLogMessage_NoTransaction_NoMessage_NoException_WithContextData_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
            });
        }

        [Test]
        public void RecordLogMessage_NoTransaction_NoMessage_NoException_NoContextData_DropsEvent()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => null;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;
            var logEvent = logEvents?.FirstOrDefault()?.Data;

            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Is.Empty);
                Assert.That(logEvent, Is.Null);
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var harvestedLogEvents = transaction.HarvestLogEvents();
            var logEvent = harvestedLogEvents.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(harvestedLogEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_NoMessage_WithException_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var harvestedLogEvents = transaction.HarvestLogEvents();
            var logEvent = harvestedLogEvents.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(harvestedLogEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_NoMessage_NoException_WithContextData_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var harvestedLogEvents = transaction.HarvestLogEvents();
            var logEvent = harvestedLogEvents.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(harvestedLogEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_NoMessage_NoException_NoContextData_DropsEvent()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            string message = null;

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => null;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var harvestedLogEvents = transaction.HarvestLogEvents();
            var logEvent = harvestedLogEvents.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(harvestedLogEvents, Is.Empty);
                Assert.That(logEvent, Is.Null);
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_ThatHasHadLogsHarvested_FallsBackToLogAggregator()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => null;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;
            transaction.HarvestLogEvents();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_NoTransaction_WithException_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var fixedStackTrace = string.Join(" \n", StackTraces.ScrubAndTruncate(exception.StackTrace, 300));
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            // Access the private collection of events to get the number of add attempts.
            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ErrorStack, Is.EqualTo(fixedStackTrace));
                Assert.That(logEvent.ErrorMessage, Is.EqualTo(exception.Message));
                Assert.That(logEvent.ErrorClass, Is.EqualTo(exception.GetType().ToString()));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_WithException_Success()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var fixedStackTrace = string.Join(" \n", StackTraces.ScrubAndTruncate(exception.StackTrace, 300));
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var harvestedLogEvents = transaction.HarvestLogEvents();
            var logEvent = harvestedLogEvents.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(harvestedLogEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ErrorStack, Is.EqualTo(fixedStackTrace));
                Assert.That(logEvent.ErrorMessage, Is.EqualTo(exception.Message));
                Assert.That(logEvent.ErrorClass, Is.EqualTo(exception.GetType().ToString()));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_WithTransaction_WithException_ThatHasHadLogsHarvested_FallsBackToLogAggregator()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(true);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var fixedStackTrace = string.Join(" \n", StackTraces.ScrubAndTruncate(exception.StackTrace, 300));
            var contextData = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;
            transaction.HarvestLogEvents();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(logEvent.TimeStamp, Is.EqualTo(timestampUnix));
                Assert.That(logEvent.Level, Is.EqualTo(level));
                Assert.That(logEvent.Message, Is.EqualTo(message));
                Assert.That(logEvent.SpanId, Is.EqualTo(spanId));
                Assert.That(logEvent.TraceId, Is.EqualTo(traceId));
                Assert.That(logEvent.ErrorStack, Is.EqualTo(fixedStackTrace));
                Assert.That(logEvent.ErrorMessage, Is.EqualTo(exception.Message));
                Assert.That(logEvent.ErrorClass, Is.EqualTo(exception.GetType().ToString()));
                Assert.That(logEvent.ContextData, Is.EqualTo(contextData));
                Assert.That(logEvent.Priority, Is.EqualTo(priority));
            });
        }

        [Test]
        public void RecordLogMessage_ContextDataDisabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(false);

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var fixedStackTrace = string.Join(" \n", StackTraces.ScrubAndTruncate(exception.StackTrace, 300));
            var contextData = new Dictionary<string, object>() {
                { "key1", "value1" },
                { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;
            transaction.HarvestLogEvents();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            var logEvent = logEvents?.FirstOrDefault()?.Data;
            Assert.Multiple(() =>
            {
                Assert.That(logEvents, Has.Count.EqualTo(1));
                Assert.That(logEvent, Is.Not.Null);
            });
            Assert.That(logEvent.ContextData, Is.Null);
        }

        [Test]
        public void RecordLogMessage_WithDenyList_DropsMessageAndIncrementsDeniedCount()
        {
            Mock.Arrange(() => _configurationService.Configuration.LogEventCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.LogMetricsCollectorEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.ContextDataEnabled)
                .Returns(false);
            Mock.Arrange(() => _configurationService.Configuration.LogLevelDenyList)
                .Returns(new HashSet<string>() { "DEBUG" });

            var timestamp = DateTime.Now;
            var timestampUnix = timestamp.ToUnixTimeMilliseconds();
            var level = "DEBUG";
            var message = "message";
            var exception = NotNewRelic.ExceptionBuilder.BuildException("exception message");
            var fixedStackTrace = string.Join(" \n", StackTraces.ScrubAndTruncate(exception.StackTrace, 300));
            var contextData = new Dictionary<string, object>() {
                { "key1", "value1" },
                { "key2", 1 } };

            Func<object, string> getLevelFunc = (l) => level;
            Func<object, DateTime> getTimestampFunc = (l) => timestamp;
            Func<object, string> getMessageFunc = (l) => message;
            Func<object, Exception> getLogExceptionFunc = (l) => exception;
            Func<object, Dictionary<string, object>> getContextDataFunc = (l) => contextData;

            var spanId = "spanid";
            var traceId = "traceid";
            var loggingFramework = "testFramework";

            SetupTransaction();
            var transaction = _transactionService.GetCurrentInternalTransaction();
            var priority = transaction.Priority;
            transaction.HarvestLogEvents();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLogMessage(loggingFramework, new object(), getTimestampFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, spanId, traceId);

            var privateAccessor = new PrivateAccessor(_logEventAggregator);
            var logEvents = privateAccessor.GetField("_logEvents") as ConcurrentPriorityQueue<PrioritizedNode<LogEventWireModel>>;

            Assert.That(logEvents, Is.Empty);
            Mock.Assert(() => _agentHealthReporter.IncrementLogDeniedCount(Arg.AnyString), Occurs.Once());


        }

        #endregion

        #region RecordLlmEvent
        [Test]
        public void RecordLlmEvent_DoesNothing_WhenAiMonitoringIsDisabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(false);

            var eventName = "eventName";
            var attributes = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventName, attributes);

            Mock.Assert(() => _transactionService.GetCurrentInternalTransaction(), Occurs.Never());
        }
        [Test]
        public void RecordLlmEvent_RecordsSupportabilityMetric_WhenAiMonitoringStreamingEnabledIsDisabled()
        {
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringStreamingEnabled)
                .Returns(false);

            var eventName = "eventName";
            var attributes = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            SetupTransaction();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventName, attributes);

            Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCountMetric("Supportability/DotNet/ML/Streaming/Disabled", 1), Occurs.Once());
        }
        [Test]
        public void RecordLlmEvent_SetsLlmTransactionMetadata()
        {
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringStreamingEnabled)
                .Returns(true);

            var eventName = "eventName";
            var attributes = new Dictionary<string, object>() { { "key1", "value1" }, { "key2", 1 } };

            SetupTransaction();

            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventName, attributes);

            var transaction = _transactionService.GetCurrentInternalTransaction();
            Assert.That(transaction.TransactionMetadata.IsLlmTransaction, Is.True);
        }

        [Test]
        public void RecordLlmEvent_IncludesCustomAttributes_WithLlmPrefix()
        {
            // Arrange
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);

            var actualAttributes = new Dictionary<string, object>();
            Mock.Arrange(() => _customEventTransformer.Transform(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<KeyValuePair<string, object>>>(), Arg.IsAny<float>()))
            .DoInstead<string, IEnumerable<KeyValuePair<string, object>>, float>((eventType, attributes, priority) =>
            {
                foreach (var attribute in attributes)
                {
                    actualAttributes.Add(attribute.Key, attribute.Value);
                }
            });

            var eventName = "eventName";

            SetupTransaction();
            _transaction.AddCustomAttribute("llm.key1", "value1");
            _transaction.AddCustomAttribute("llm.key2", 1);
            _transaction.AddCustomAttribute("key3", "value3");

            // Act
            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventName, new Dictionary<string, object>());

            var expectedAttributes = new Dictionary<string, object>() { { "llm.key1", "value1" }, { "llm.key2", 1 } };
            Assert.That(actualAttributes, Is.EqualTo(expectedAttributes));
        }
        [Test]
        [TestCase("LlmChatCompletionMessage")]
        [TestCase("LlmEmbedding")]
        public void RecordLlmEvent_SetsTokenCount_IfLlmTokenCountingCallback_IsSetAndReturnsGreaterThanZero(string eventType)
        {
            // Arrange
            var actualAttributes = new Dictionary<string, object>();
            string actualModel = null;
            Mock.Arrange(() => _customEventTransformer.Transform(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<KeyValuePair<string, object>>>(), Arg.IsAny<float>()))
                .DoInstead<string, IEnumerable<KeyValuePair<string, object>>, float>((eventType, attributes, priority) =>
                {
                    foreach (var attribute in attributes)
                    {
                        actualAttributes.Add(attribute.Key, attribute.Value);
                    }
                });

            Mock.Arrange(() => _configurationService.Configuration.LlmTokenCountingCallback).Returns((model, content) =>
            {
                actualModel = model;
                if (model == "model1" && content == "This is a test message")
                {
                    return 10;
                }
                return 0;
            });
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);

            var modelAttribute = eventType == "LlmChatCompletionMessage" ? "request.model" : "response.model";
            var attributes = new Dictionary<string, object>
            {
                { eventType == "LlmChatCompletionMessage" ? "content" : "input", "This is a test message" },
                { modelAttribute, "model1" }
            };

            SetupTransaction();

            // Act
            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventType, attributes);

            // Assert
            Assert.That(actualAttributes["token_count"], Is.EqualTo(10));
            Assert.That(actualModel, Is.EqualTo("model1"));
        }
        [Test]
        public void RecordLlmEvent_DoesNotSetTokenCount_IfLlmTokenCountingCallback_IsNotSet()
        {
            // Arrange
            var actualAttributes = new Dictionary<string, object>();
            Mock.Arrange(() => _customEventTransformer.Transform(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<KeyValuePair<string, object>>>(), Arg.IsAny<float>()))
                .DoInstead<string, IEnumerable<KeyValuePair<string, object>>, float>((eventType, attributes, priority) =>
                {
                    foreach (var attribute in attributes)
                    {
                        actualAttributes.Add(attribute.Key, attribute.Value);
                    }
                });

            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);

            var attributes = new Dictionary<string, object>
            {
                { "content", "This is a test message" },
                { "request.model", "model1" }
            };

            SetupTransaction();

            // Act
            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent("LlmChatCompletionMessage", attributes);

            // Assert
            Assert.That(actualAttributes.ContainsKey("token_count"), Is.False);
        }

        [Test]
        public void RecordLlmEvent_DoesNotSetTokenCount_IfLlmTokenCountingCallback_ReturnsZero()
        {
            // Arrange
            var actualAttributes = new Dictionary<string, object>();
            Mock.Arrange(() => _customEventTransformer.Transform(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<KeyValuePair<string, object>>>(), Arg.IsAny<float>()))
                .DoInstead<string, IEnumerable<KeyValuePair<string, object>>, float>((eventType, attributes, priority) =>
                {
                    foreach (var attribute in attributes)
                    {
                        actualAttributes.Add(attribute.Key, attribute.Value);
                    }
                });

            Mock.Arrange(() => _configurationService.Configuration.LlmTokenCountingCallback).Returns((model, content) => 0);
            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                .Returns(true);

            var attributes = new Dictionary<string, object>
            {
                { "content", "This is a test message" },
                { "request.model", "model1" }
            };

            SetupTransaction();

            // Act
            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent("LlmChatCompletionMessage", attributes);

            // Assert
            Assert.That(actualAttributes.ContainsKey("token_count"), Is.False);
        }

        [Test]
        [TestCase("LlmChatCompletionMessage", "content", true, false)]
        [TestCase("LlmChatCompletionMessage", "content", false, true)]
        [TestCase("LlmEmbedding", "input", true, false)]
        [TestCase("LlmEmbedding", "input", false, true)]
        public void RecordLlmEvent_RemovesAttribute_IfHighSecurityMode_IsEnabled_OrRecordContentIsDisabled(string eventType, string attributeName, bool highSecurityMode, bool recordContentEnabled)
        {
            // Arrange
            var actualAttributes = new Dictionary<string, object>();
            Mock.Arrange(() => _customEventTransformer.Transform(Arg.IsAny<string>(), Arg.IsAny<IEnumerable<KeyValuePair<string, object>>>(), Arg.IsAny<float>()))
                .DoInstead<string, IEnumerable<KeyValuePair<string, object>>, float>((_, attributes, priority) =>
                {
                    foreach (var attribute in attributes)
                    {
                        actualAttributes.Add(attribute.Key, attribute.Value);
                    }
                });

            if (highSecurityMode)
                Mock.Arrange(() => _configurationService.Configuration.HighSecurityModeEnabled)
                    .Returns(true);
            if (!recordContentEnabled)
                Mock.Arrange(() => _configurationService.Configuration.AiMonitoringRecordContentEnabled)
                    .Returns(false);

            Mock.Arrange(() => _configurationService.Configuration.AiMonitoringEnabled)
                    .Returns(true);

            var attributes = new Dictionary<string, object>
            {
                { attributeName, "This is a test message" },
                { "request.model", "model1" }
            };

            SetupTransaction();

            // Act
            var xapi = _agent as IAgentExperimental;
            xapi.RecordLlmEvent(eventType, attributes);

            // Assert
            Assert.That(actualAttributes.ContainsKey(attributeName), Is.False);
        }
        #endregion

        private void SetupTransaction()
        {
            var transactionName = TransactionName.ForWebTransaction("foo", "bar");
            _transaction = new Transaction(_configurationService.Configuration, transactionName, Mock.Create<ISimpleTimer>(), DateTime.UtcNow, _callStackManager, Mock.Create<IDatabaseService>(), default(float), Mock.Create<IDatabaseStatementParser>(), _distributedTracePayloadHandler, _errorService, _attribDefs);

            Mock.Arrange(() => _transactionService.GetCurrentInternalTransaction()).Returns(_transaction);
        }
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
