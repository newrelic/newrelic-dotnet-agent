using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions.Collections.Generic;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
    public class AgentWrapperApi : IAgentWrapperApi // any changes to api, update the interface in extensions and re-import, then implement in legacy api as NotImplementedException
    {
        internal static readonly Int32 MaxSegmentLength = 255;
        private readonly ITransactionService _transactionService;
        private readonly ITimerFactory _timerFactory;
        private readonly ITransactionTransformer _transactionTransformer;
        private readonly IThreadPoolStatic _threadPoolStatic;
        private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;
        private readonly IPathHashMaker _pathHashMaker;
        private readonly ICatHeaderHandler _catHeaderHandler;
        private readonly ISyntheticsHeaderHandler _syntheticsHeaderHandler;
        private readonly ITransactionFinalizer _transactionFinalizer;
        private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
        private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
        private readonly IConfigurationService _configurationService;

        private readonly IAgentHealthReporter _agentHealthReporter;
        private static readonly Extensions.Providers.Wrapper.ITransaction _noOpTransaction = new NoTransactionImpl();
        private static readonly ISegment _noOpSegment = new NoTransactionImpl();

        public AgentWrapperApi(ITransactionService transactionService, ITimerFactory timerFactory, ITransactionTransformer transactionTransformer, IThreadPoolStatic threadPoolStatic, ITransactionMetricNameMaker transactionMetricNameMaker, IPathHashMaker pathHashMaker, ICatHeaderHandler catHeaderHandler, ISyntheticsHeaderHandler syntheticsHeaderHandler, ITransactionFinalizer transactionFinalizer, IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter)
        {
            _transactionService = transactionService;
            _timerFactory = timerFactory;
            _transactionTransformer = transactionTransformer;
            _threadPoolStatic = threadPoolStatic;
            _transactionMetricNameMaker = transactionMetricNameMaker;
            _pathHashMaker = pathHashMaker;
            _catHeaderHandler = catHeaderHandler;
            _syntheticsHeaderHandler = syntheticsHeaderHandler;
            _transactionFinalizer = transactionFinalizer;
            _browserMonitoringPrereqChecker = browserMonitoringPrereqChecker;
            _browserMonitoringScriptMaker = browserMonitoringScriptMaker;
            _configurationService = configurationService;
            _agentHealthReporter = agentHealthReporter;
        }

        #region Transaction management

        public Extensions.Providers.Wrapper.ITransaction CreateWebTransaction(WebTransactionType type, string transactionDisplayName, bool mustBeRootTransaction, Action wrapperOnCreate)
        {
            if (transactionDisplayName == null)
                throw new ArgumentNullException("transactionDisplayName");

            Action onCreate = () =>
            {
                wrapperOnCreate?.Invoke();
            };

            var initialTransactionName = new WebTransactionName(Enum.GetName(typeof(WebTransactionType), type), transactionDisplayName);
            return new Transaction(this, _transactionService.GetOrCreateInternalTransaction(initialTransactionName, onCreate, mustBeRootTransaction));
        }

        public Extensions.Providers.Wrapper.ITransaction CreateMessageBrokerTransaction(MessageBrokerDestinationType destinationType, String brokerVendorName, String destination, Action wrapperOnCreate)
        {
            if (brokerVendorName == null)
                throw new ArgumentNullException("brokerVendorName");

            Action onCreate = () =>
            {
                wrapperOnCreate?.Invoke();
            };

            var initialTransactionName = new MessageBrokerTransactionName(destinationType.ToString(), brokerVendorName, destination);
            return new Transaction(this, _transactionService.GetOrCreateInternalTransaction(initialTransactionName, onCreate, true));
        }

        public Extensions.Providers.Wrapper.ITransaction CreateOtherTransaction(string category, string name, bool mustBeRootTransaction, Action wrapperOnCreate)
        {
            if (category == null)
                throw new ArgumentNullException("category");
            if (name == null)
                throw new ArgumentNullException("name");

            Action onCreate = () =>
            {
                wrapperOnCreate?.Invoke();
            };

            var initialTransactionName = new OtherTransactionName(category, name);
            return new Transaction(this, _transactionService.GetOrCreateInternalTransaction(initialTransactionName, onCreate, mustBeRootTransaction));
        }

        public Extensions.Providers.Wrapper.ITransaction CurrentTransaction
        {
            get
            {
                var transaction = _transactionService.GetCurrentInternalTransaction();
                return transaction == null ? _noOpTransaction : new Transaction(this, transaction);
            }
        }

        public void EndTransaction(ITransaction transaction)
        {
            transaction.NoticeUnitOfWorkEnds();

            // There is still work to do, so delay ending transaction until there no more work left
            if (transaction.UnitOfWorkCount > 0)
                return;

            // We want to finish then transaction as fast as possible to get the more accurate possible response time
            _transactionFinalizer.Finish(transaction);

            // We also want to remove the transaction from the transaction context before returning so that it won't be reused
            _transactionService.RemoveOutstandingInternalTransactions(true);

            Action transformWork = () => _transactionTransformer.Transform(transaction);
            // The completion of transactions can be run on thread or off thread. We made this configurable.  
            if (_configurationService.Configuration.CompleteTransactionsOnThread)
            {
                transformWork.CatchAndLog();
            }
            else
            {
                ExecuteOffRequestThread(transformWork);
            }
        }

        #endregion Transaction management

        #region Transaction Mutation
        private static String GetTransactionName(String path)
        {
            if (path.StartsWith("/"))
                path = path.Substring(1);

            if (path == String.Empty)
                path = "Root";

            return path;
        }

        #endregion Transaction Mutation

        #region Transaction segment managements

        public ISegment CastAsSegment(Object segment)
        {
            return segment as ISegment ?? _noOpSegment;
        }

        public void EnableExplainPlans(ISegment segment, Func<object> allocateExplainPlanResources, Func<object, ExplainPlan> generateExplainPlan, Func<VendorExplainValidationResult> vendorValidateShouldExplain)
        {
            if (!_configurationService.Configuration.SqlExplainPlansEnabled || !segment.IsValid)
            {
                return;
            }

            var datastoreSegment = segment as TypedSegment<DatastoreSegmentData>;
            if (datastoreSegment == null)
            {
                throw new ArgumentException("Received a datastore segment object which was not of expected type");
            }

            var data = datastoreSegment.TypedData;
            data.GetExplainPlanResources = allocateExplainPlanResources;
            data.GenerateExplainPlan = generateExplainPlan;
            data.DoExplainPlanCondition = ShouldRunExplain;

            // Ensure the condition is not called until after the segment is finished (to get accurate duration)
            bool ShouldRunExplain()
            {
                var shouldRunExplainPlan = _configurationService.Configuration.SqlExplainPlansEnabled &&
                                           datastoreSegment.Duration >= _configurationService.Configuration.SqlExplainPlanThreshold;

                if (!shouldRunExplainPlan)
                {
                    return false;
                }

                if (vendorValidateShouldExplain != null)
                {
                    var vendorValidationResult = vendorValidateShouldExplain();
                    if (!vendorValidationResult.IsValid)
                    {
                        Log.DebugFormat("Failed vendor condition for executing explain plan: {0}", vendorValidationResult.ValidationMessage);
                        return false;
                    }
                }

                return true;
            }
        }

        private static MethodCallData GetMethodCallData(MethodCall methodCall)
        {
            var typeName = methodCall.Method.Type.FullName ?? "[unknown]";
            var methodName = methodCall.Method.MethodName;
            var invocationTargetHashCode = RuntimeHelpers.GetHashCode(methodCall.InvocationTarget);
            return new MethodCallData(typeName, methodName, invocationTargetHashCode);
        }

        #endregion Transaction segment management

        #region outbound CAT request, inbound CAT response

        private IEnumerable<KeyValuePair<String, String>> GetOutboundRequestHeaders(ITransaction transaction)
        {
            var headers = Enumerable.Empty<KeyValuePair<String, String>>();

            var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
            var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

            UpdatePathHash(transaction, currentTransactionMetricName);

            return headers
                .Concat(_catHeaderHandler.TryGetOutboundRequestHeaders(transaction))
                .Concat(_syntheticsHeaderHandler.TryGetOutboundSyntheticsRequestHeader(transaction));
        }

        #endregion outbound CAT request, inbound CAT response

        #region inbound CAT request, outbound CAT response

        public void ProcessInboundRequest(IEnumerable<KeyValuePair<String, String>> headers, long? contentLength)
        {
            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            var transaction = _transactionService.GetCurrentInternalTransaction();
            if (transaction == null)
                return;

            // NOTE: the key for this dictionary should NOT be case sensitive since HTTP headers are not case sensitive.
            // See: https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
            var headerDictionary = headers.ToDictionary(equalityComparer: StringComparer.OrdinalIgnoreCase);
            TryProcessCatRequestData(transaction, headerDictionary, contentLength);
            TryProcessSyntheticsData(transaction, headerDictionary);

        }

        private void TryProcessCatRequestData(ITransaction transaction, IDictionary<String, String> headers, long? contentLength)
        {
            var referrerCrossApplicationProcessId = GetReferrerCrossApplicationProcessId(transaction, headers);
            if (referrerCrossApplicationProcessId == null)
                return;

            UpdateReferrerCrossApplicationProcessId(transaction, referrerCrossApplicationProcessId);

            var crossApplicationRequestData = _catHeaderHandler.TryDecodeInboundRequestHeaders(headers);
            if (crossApplicationRequestData == null)
                return;

            UpdateTransactionMetadata(transaction, crossApplicationRequestData, contentLength);
        }

        private void TryProcessSyntheticsData(ITransaction transaction, IDictionary<String, String> headers)
        {
            var syntheticsRequestData = _syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(headers);
            if (syntheticsRequestData == null)
                return;

            UpdateTransactionMetaData(transaction, syntheticsRequestData);
        }

        private IEnumerable<KeyValuePair<String, String>> GetOutboundResponseHeaders(ITransaction transaction)
        {
            var headers = Enumerable.Empty<KeyValuePair<String, String>>();

            // A CAT response header should only be sent if we had a valid CAT inbound request
            if (transaction.TransactionMetadata.CrossApplicationReferrerProcessId == null)
            {
                return headers;
            }

            // freeze transaction name so it doesn't change after we report it back to the caller app that made the external request
            transaction.CandidateTransactionName.Freeze();

            var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
            var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

            UpdatePathHash(transaction, currentTransactionMetricName);

            //We should only add outbound CAT headers here (not synthetics) given that this is a Response to a request
            return headers
                .Concat(_catHeaderHandler.TryGetOutboundResponseHeaders(transaction, currentTransactionMetricName));
        }

        #endregion inbound CAT request, outboud CAT response

        #region Error handling

        public void HandleWrapperException(Exception exception)
        {
            // This method should never throw
            if (exception == null)
                return;

            Log.Error($"An exception occurred in a wrapper: {exception}");
        }

        #endregion Error handling

        #region Stream manipulation

        public Stream TryGetStreamInjector(Stream stream, Encoding encoding, String contentType, String requestPath)
        {
            if (stream == null)
                return null;
            if (encoding == null)
                return null;
            if (contentType == null)
                return null;
            if (requestPath == null)
                return null;

            try
            {
                var transaction = _transactionService.GetCurrentInternalTransaction();
                if (transaction == null)
                    return null;

                var shouldInject = _browserMonitoringPrereqChecker.ShouldAutomaticallyInject(transaction, requestPath, contentType);
                if (!shouldInject)
                    return null;

                // Once the transaction name is used for RUM it must be frozen
                transaction.CandidateTransactionName.Freeze();

                var script = _browserMonitoringScriptMaker.GetScript(transaction);
                if (script == null)
                    return null;

                return new BrowserMonitoringStreamInjector(() => script, stream, encoding);
            }
            catch (Exception ex)
            {
                Log.Error($"RUM: Failed to build Browser Monitoring agent script: {ex}");
                return null;
            }
        }

        #endregion Stream manipulation

        #region Helpers

        private void ExecuteOffRequestThread(Action action)
        {
            _threadPoolStatic.QueueUserWorkItem(_ => action.CatchAndLog());
        }

        private String GetReferrerCrossApplicationProcessId(ITransaction transaction, IDictionary<String, String> headers)
        {
            var existingReferrerProcessId = transaction.TransactionMetadata.CrossApplicationReferrerProcessId;
            if (existingReferrerProcessId != null)
            {
                Log.Warn($"Already received inbound cross application request with referrer cross process id: {existingReferrerProcessId}");
                return null;
            }

            return _catHeaderHandler?.TryDecodeInboundRequestHeadersForCrossProcessId(headers);
        }

        private void UpdatePathHash(ITransaction transaction, TransactionMetricName transactionMetricName)
        {
            var pathHash = _pathHashMaker.CalculatePathHash(transactionMetricName.PrefixedName, transaction.TransactionMetadata.CrossApplicationReferrerPathHash);
            transaction.TransactionMetadata.SetCrossApplicationPathHash(pathHash);
        }

        private void UpdateReferrerCrossApplicationProcessId(ITransaction transaction, String referrerCrossApplicationProcessId)
        {
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(referrerCrossApplicationProcessId);
        }

        private void UpdateTransactionMetadata(ITransaction transaction, CrossApplicationRequestData crossApplicationRequestData, long? contentLength)
        {
            if (crossApplicationRequestData.TripId != null)
                transaction.TransactionMetadata.SetCrossApplicationReferrerTripId(crossApplicationRequestData.TripId);
            if (contentLength != null && contentLength.Value > 0)
                transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(contentLength.Value);
            if (crossApplicationRequestData.PathHash != null)
                transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash(crossApplicationRequestData.PathHash);
            if (crossApplicationRequestData.TransactionGuid != null)
                transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid(crossApplicationRequestData.TransactionGuid);
        }

        private void UpdateTransactionMetaData(ITransaction transaction, SyntheticsHeader syntheticsHeader)
        {
            transaction.TransactionMetadata.SetSyntheticsResourceId(syntheticsHeader.ResourceId);
            transaction.TransactionMetadata.SetSyntheticsJobId(syntheticsHeader.JobId);
            transaction.TransactionMetadata.SetSyntheticsMonitorId(syntheticsHeader.MonitorId);
        }

        private static MetricNames.MessageBrokerDestinationType AgentWrapperApiEnumToMetricNamesEnum(MessageBrokerDestinationType wrapper)
        {
            switch (wrapper)
            {
                case MessageBrokerDestinationType.Queue:
                    return MetricNames.MessageBrokerDestinationType.Queue;
                case MessageBrokerDestinationType.Topic:
                    return MetricNames.MessageBrokerDestinationType.Topic;
                case MessageBrokerDestinationType.TempQueue:
                    return MetricNames.MessageBrokerDestinationType.TempQueue;
                case MessageBrokerDestinationType.TempTopic:
                    return MetricNames.MessageBrokerDestinationType.TempTopic;
                default:
                    throw new InvalidOperationException("Unexpected enum value: " + wrapper);
            }
        }

        private static MetricNames.MessageBrokerAction AgentWrapperApiEnumToMetricNamesEnum(MessageBrokerAction wrapper)
        {
            switch (wrapper)
            {
                case MessageBrokerAction.Consume:
                    return MetricNames.MessageBrokerAction.Consume;
                case MessageBrokerAction.Peek:
                    return MetricNames.MessageBrokerAction.Peek;
                case MessageBrokerAction.Produce:
                    return MetricNames.MessageBrokerAction.Produce;
                case MessageBrokerAction.Purge:
                    return MetricNames.MessageBrokerAction.Purge;
                default:
                    throw new InvalidOperationException("Unexpected enum value: " + wrapper);
            }
        }

        private void AttachToAsync(ITransaction transaction)
        {
            var isAdded = _transactionService.SetTransactionOnAsyncContext(transaction);
            if (!isAdded)
            {
                Log.Debug("Failed to add transaction to async storage. This can occur when there are no asyn context providers.");
            }
        }

        private void Detach(bool removeAsync)
        {
            _transactionService.RemoveOutstandingInternalTransactions(removeAsync);
        }

        #endregion

        /// <summary>
        /// This is a simple shim.  It cannot keep any state.  We could potentially remove the
        /// need for this class by implementing the extension ITransaction interface in the agent's
        /// transaction implementation.
        /// </summary>
        private sealed class Transaction : Extensions.Providers.Wrapper.ITransaction
        {
            private readonly AgentWrapperApi agentWrapperApi;
            private readonly ITransaction transaction;

            public bool IsValid => true;

            public Transaction(AgentWrapperApi agentWrapperApi, ITransaction transaction)
            {
                this.agentWrapperApi = agentWrapperApi;
                this.transaction = transaction;
            }

            public void End()
            {
                if (transaction.CandidateTransactionName.CurrentTransactionName.IsWeb && transaction.TransactionMetadata.HttpResponseStatusCode >= 400)
                {
                    SetWebTransactionName(WebTransactionType.StatusCode, $"{transaction.TransactionMetadata.HttpResponseStatusCode}", 2);
                }

                agentWrapperApi.EndTransaction(transaction);
            }

            public void Dispose()
            {
                End();
            }

            public ISegment StartCustomSegment(MethodCall methodCall, string segmentName)
            {
                if (transaction.Ignored)
                    return _noOpSegment;
                if (segmentName == null)
                    throw new ArgumentNullException(nameof(segmentName));

                // Note: In our public docs to tells users that they must prefix their metric names with "Custom/", but there's no mechanism that actually enforces this restriction, so there's no way to know whether it'll be there or not. For consistency, we'll just strip off "Custom/" if there's at all and then we know it's consistently not there.
                if (segmentName.StartsWith("Custom/"))
                    segmentName = segmentName.Substring(7);
                segmentName = Clamper.ClampLength(segmentName.Trim(), MaxSegmentLength);
                if (segmentName.Length <= 0)
                    throw new ArgumentException("A segment name cannot be an empty string.");

                var methodCallData = GetMethodCallData(methodCall);

                return new TypedSegment<CustomSegmentData>(transaction.TransactionSegmentState, methodCallData, new CustomSegmentData(segmentName));
            }

            public ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
            {
                if (transaction.Ignored)
                    return _noOpSegment;
                if (brokerVendorName == null)
                    throw new ArgumentNullException("brokerVendorName");

                var action = AgentWrapperApi.AgentWrapperApiEnumToMetricNamesEnum(operation);
                var destType = AgentWrapperApi.AgentWrapperApiEnumToMetricNamesEnum(destinationType);
                var methodCallData = GetMethodCallData(methodCall);
                return new TypedSegment<MessageBrokerSegmentData>(transaction.TransactionSegmentState, methodCallData,
                    new MessageBrokerSegmentData(brokerVendorName, destinationName, destType, action));
            }

            public ISegment StartDatastoreSegment(MethodCall methodCall, String operation, DatastoreVendor datastoreVendorName, String model, String commandText, String host = null, String portPathOrId = null, String databaseName = null)
            {
                if (transaction.Ignored)
                    return _noOpSegment;

                var data = new DatastoreSegmentData()
                {
                    Operation = operation,
                    DatastoreVendorName = datastoreVendorName,
                    Model = model,
                    CommandText = commandText,
                    Host = string.IsNullOrEmpty(host) ? "unknown" : host,
                    PortPathOrId = string.IsNullOrEmpty(portPathOrId) ? "unknown" : portPathOrId,
                    DatabaseName = string.IsNullOrEmpty(databaseName) ? "unknown" : databaseName
                };
                var segment = new TypedSegment<DatastoreSegmentData>(transaction.TransactionSegmentState, GetMethodCallData(methodCall), data);

                return segment;
            }


            public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, String method)
            {
                if (transaction.Ignored)
                    return _noOpSegment;
                if (destinationUri == null)
                    throw new ArgumentNullException(nameof(destinationUri));
                if (method == null)
                    throw new ArgumentNullException(nameof(method));
                if (!destinationUri.IsAbsoluteUri)
                    throw new ArgumentException($"Must use an absolute URI, not a relative URI", nameof(destinationUri));

                var methodCallData = GetMethodCallData(methodCall);
                return new TypedSegment<ExternalSegmentData>(transaction.TransactionSegmentState, methodCallData,
                    new ExternalSegmentData(destinationUri, method));
            }

            public ISegment StartMethodSegment(MethodCall methodCall, String typeName, String methodName)
            {
                if (transaction.Ignored)
                    return _noOpSegment;
                if (typeName == null)
                    throw new ArgumentNullException(nameof(typeName));
                if (methodName == null)
                    throw new ArgumentNullException(nameof(methodName));

                var methodCallData = GetMethodCallData(methodCall);
                return new TypedSegment<MethodSegmentData>(transaction.TransactionSegmentState, methodCallData, new MethodSegmentData(typeName, methodName));
            }

            public ISegment StartTransactionSegment(MethodCall methodCall, String segmentDisplayName)
            {
                if (transaction.Ignored)
                    return _noOpSegment;
                if (segmentDisplayName == null)
                    throw new ArgumentNullException("segmentDisplayName");

                var methodCallData = GetMethodCallData(methodCall);
                return new TypedSegment<SimpleSegmentData>(transaction.TransactionSegmentState, methodCallData, new SimpleSegmentData(segmentDisplayName));
            }

            public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
            {
                return agentWrapperApi.GetOutboundRequestHeaders(transaction);
            }

            public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
            {
                return agentWrapperApi.GetOutboundResponseHeaders(transaction);
            }
            public void NoticeError(Exception exception)
            {
                Log.Debug($"Noticed application error: {exception}");

                var stripErrorMessage = agentWrapperApi._configurationService.Configuration.HighSecurityModeEnabled;
                var errorData = ErrorData.FromException(exception, stripErrorMessage);

                transaction.TransactionMetadata.AddExceptionData(errorData);
            }

            public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null)
            {
                transaction.TransactionMetadata.SetHttpResponseStatusCode(statusCode, subStatusCode);
            }

            public void AttachToAsync()
            {
                agentWrapperApi.AttachToAsync(transaction);
                transaction.CallStackManager.AttachToAsync();
            }

            public void Detach()
            {
                agentWrapperApi.Detach(true);
            }

            public void DetachFromPrimary()
            {
                agentWrapperApi.Detach(false);
            }

            public void ProcessInboundResponse(IEnumerable<KeyValuePair<string, string>> headers, ISegment segment)
            {
                if (segment == null || !segment.IsValid)
                {
                    return;
                }

                var externalSegment = segment as TypedSegment<ExternalSegmentData>;
                if (externalSegment == null)
                {
                    throw new Exception(
                        $"Expected segment of type {typeof(TypedSegment<ExternalSegmentData>).FullName} but received segment of type {segment.GetType().FullName}");
                }

                // NOTE: the key for this dictionary should NOT be case sensitive since HTTP headers are not case sensitive.
                // See: https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
                var headerDictionary = headers.ToDictionary(equalityComparer: StringComparer.OrdinalIgnoreCase);
                var responseData = agentWrapperApi._catHeaderHandler.TryDecodeInboundResponseHeaders(headerDictionary);
                externalSegment.TypedData.CrossApplicationResponseData = responseData;

                if (responseData != null)
                {
                    transaction?.TransactionMetadata.MarkHasCatResponseHeaders();
                }
            }

            public void Hold()
            {
                transaction.NoticeUnitOfWorkBegins();
            }

            public void Release()
            {
                End();
            }

            public void SetWebTransactionName(WebTransactionType type, string name, int priority = 1)
            {
                var transactionName = new WebTransactionName(Enum.GetName(typeof(WebTransactionType), type), name);
                transaction.CandidateTransactionName.TrySet(transactionName, priority);
            }

            public void SetWebTransactionNameFromPath(WebTransactionType type, string path)
            {
                var cleanedPath = GetTransactionName(path);
                var transactionName = new UriTransactionName(cleanedPath);
                transaction.CandidateTransactionName.TrySet(transactionName, 1);
            }

            public void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, int priority = 1)
            {
                var transactionName = new MessageBrokerTransactionName(destinationType.ToString(), brokerVendorName, destination);
                transaction.CandidateTransactionName.TrySet(transactionName, priority);
            }

            public void SetOtherTransactionName(string category, string name, int priority = 1)
            {
                var transactionName = new OtherTransactionName(category, name);
                transaction.CandidateTransactionName.TrySet(transactionName, priority);
            }

            public void SetCustomTransactionName(string name, int priority = 1)
            {
                // Note: In our public docs to tells users that they must prefix their metric names with "Custom/", but there's no mechanism that actually enforces this restriction, so there's no way to know whether it'll be there or not. For consistency, we'll just strip off "Custom/" if there's at all and then we know it's consistently not there.
                if (name.StartsWith("Custom/"))
                {
                    name = name.Substring(7);
                }

                name = Clamper.ClampLength(name.Trim(), MaxSegmentLength);
                if (name.Length <= 0)
                {
                    throw new ArgumentException("A segment name cannot be an empty string.");
                }

                // Determine if the current best transaction name is is a web name and, if so, stay in that namespace.
                var isweb = transaction.CandidateTransactionName.CurrentTransactionName.IsWeb;

                var transactionName = new CustomTransactionName(name, isweb);
                transaction.CandidateTransactionName.TrySet(transactionName, priority);
            }

            public void SetUri(string uri)
            {
                if (uri == null)
                {
                    throw new ArgumentNullException(nameof(uri));
                }

                var cleanUri = Strings.CleanUri(uri);

                transaction.TransactionMetadata.SetUri(cleanUri);
            }

            public void SetOriginalUri(string uri)
            {
                if (uri == null)
                {
                    throw new ArgumentNullException(nameof(uri));
                }

                var cleanUri = Strings.CleanUri(uri);

                transaction.TransactionMetadata.SetOriginalUri(cleanUri);
            }

            public void SetPath(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException(nameof(path));
                }

                transaction.TransactionMetadata.SetPath(path);
            }

            public void SetReferrerUri(string uri)
            {
                if (uri == null)
                {
                    throw new ArgumentNullException(nameof(uri));
                }

                // Always strip query string parameters on a referrer. Per https://newrelic.atlassian.net/browse/DOTNET-2141
                var cleanUri = Strings.CleanUri(uri);

                transaction.TransactionMetadata.SetReferrerUri(cleanUri);
            }

            public void SetQueueTime(TimeSpan queueTime)
            {
                if (queueTime == null)
                {
                    throw new ArgumentNullException(nameof(queueTime));
                }

                transaction.TransactionMetadata.SetQueueTime(queueTime);
            }

            public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters, RequestParameterBucket bucket)
            {
                if (parameters == null)
                {
                    throw new ArgumentNullException(nameof(parameters));
                }

                MoreEnumerable.ForEach(parameters
                    .Where(parameter => parameter.Key != null)
                    .Where(parameter => parameter.Value != null), parameter =>
                {
                    switch (bucket)
                    {
                        case RequestParameterBucket.RequestParameters:
                            transaction.TransactionMetadata.AddRequestParameter(parameter.Key, parameter.Value);
                            return;
                        case RequestParameterBucket.ServiceRequest:
                            transaction.TransactionMetadata.AddServiceParameter(parameter.Key, parameter.Value);
                            return;
                    }
                });
            }

            public object GetOrSetValueFromCache(string key, Func<object> func)
            {
                return transaction.GetOrSetValueFromCache(key, func);
            }

            public void Ignore()
            {
                transaction.Ignore();

                if (Log.IsFinestEnabled)
                {
                    var transactionName = transaction.CandidateTransactionName.CurrentTransactionName;
                    var transactionMetricName = agentWrapperApi._transactionMetricNameMaker.GetTransactionMetricName(transactionName);
                    var stackTrace = new StackTrace();
                    Log.Finest($"Transaction \"{transactionMetricName}\" is being ignored from {stackTrace}");
                }
            }

            public ParsedSqlStatement GetParsedDatabaseStatement(CommandType commandType, string sql)
            {
                return transaction.GetParsedDatabaseStatement(commandType, sql);
            }
        }

        private sealed class NoTransactionImpl : Extensions.Providers.Wrapper.ITransaction, ISegment
        {
            public bool IsValid => false;

            public void End()
            {
            }

            public void Dispose()
            {
            }

            public void End(Exception ex)
            {
            }

            public ISegment StartCustomSegment(MethodCall methodCall, string segmentName)
            {
#if DEBUG
                Log.Finest("Skipping StartCustomSegment outside of a transaction");
#endif
                return this;
            }

            public ISegment StartDatastoreSegment(MethodCall methodCall, string operation, DatastoreVendor datastoreVendorName, string model, string commandText, string host = null, string portPathOrId = null, string databaseName = null)
            {
#if DEBUG
                Log.Finest("Skipping StartDatastoreSegment outside of a transaction");
#endif
                return this;
            }

            public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method)
            {
#if DEBUG
                Log.Finest("Skipping StartExternalRequestSegment outside of a transaction");
#endif
                return this;
            }

            public ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
            {
#if DEBUG
                Log.Finest("Skipping StartMessageBrokerSegment outside of a transaction");
#endif
                return this;
            }

            public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName)
            {
#if DEBUG
                Log.Finest("Skipping StartMethodSegment outside of a transaction");
#endif
                return this;
            }

            public ISegment StartTransactionSegment(MethodCall methodCall, string segmentDisplayName)
            {
#if DEBUG
                Log.Finest("Skipping StartTransactionSegment outside of a transaction");
#endif
                return this;
            }

            public void MakeCombinable()
            {
            }

            public void RemoveSegmentFromCallStack()
            {
            }

            public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
            {
                Log.Debug("Tried to retrieve CAT response metadata, but there was no transaction");

                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
            {
                Log.Debug("Tried to retrieve CAT request metadata, but there was no transaction");

                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            public void NoticeError(Exception exception)
            {
                Log.Debug($"Ignoring application error because it occurred outside of a transaction: {exception}");
            }

            public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null)
            {

            }

            public void AttachToAsync()
            {
            }

            public void Detach()
            {
            }

            public void DetachFromPrimary()
            {
            }

            public void ProcessInboundResponse(IEnumerable<KeyValuePair<string, string>> headers, ISegment segment)
            {

            }

            public void Hold()
            {

            }

            public void Release()
            {

            }

            public void SetWebTransactionName(WebTransactionType type, string name, int priority = 1)
            {

            }

            public void SetWebTransactionNameFromPath(WebTransactionType type, string path)
            {

            }

            public void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, int priority = 1)
            {

            }

            public void SetOtherTransactionName(string category, string name, int priority = 1)
            {

            }

            public void SetCustomTransactionName(string name, int priority = 1)
            {

            }

            public void SetUri(string uri)
            {

            }

            public void SetOriginalUri(string uri)
            {

            }

            public void SetPath(string path)
            {

            }

            public void SetReferrerUri(string uri)
            {

            }

            public void SetQueueTime(TimeSpan queueTime)
            {

            }

            public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters, RequestParameterBucket bucket)
            {

            }

            public object GetOrSetValueFromCache(string key, Func<object> func)
            {
                return null;
            }

            public void Ignore()
            {
            }

            public ParsedSqlStatement GetParsedDatabaseStatement(CommandType commandType, string sql)
            {
                return null;
            }
        }
    }
}
