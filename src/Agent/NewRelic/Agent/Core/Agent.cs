// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.SystemInterfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NewRelic.Agent.Core
{
    public class Agent : IAgent // any changes to api, update the interface in extensions and re-import, then implement in legacy api as NotImplementedException
    {
        public const int QueryParameterMaxStringLength = 256;
        internal static Agent Instance;
        private static readonly ITransaction _noOpTransaction = new NoOpTransaction();

        // These fields should all be made private. The ones that are currently internal are being used elsewhere in code and require refactoring.
        internal readonly ITransactionService _transactionService;
        internal readonly ITransactionTransformer _transactionTransformer;
        internal readonly IThreadPoolStatic _threadPoolStatic;
        internal readonly ITransactionMetricNameMaker _transactionMetricNameMaker;
        internal readonly IPathHashMaker _pathHashMaker;
        internal readonly ICatHeaderHandler _catHeaderHandler;
        internal readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        internal readonly ISyntheticsHeaderHandler _syntheticsHeaderHandler;
        internal readonly ITransactionFinalizer _transactionFinalizer;
        private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
        private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
        private readonly IConfigurationService _configurationService;
        internal readonly IAgentHealthReporter _agentHealthReporter;
        internal readonly IAgentTimerService _agentTimerService;
        internal readonly IMetricNameService _metricNameService;
        private readonly ICATSupportabilityMetricCounters _catMetricCounters;
        private readonly Api.ITraceMetadataFactory _traceMetadataFactory;
        private Extensions.Logging.ILogger _logger;

        public Agent(ITransactionService transactionService, ITransactionTransformer transactionTransformer,
            IThreadPoolStatic threadPoolStatic, ITransactionMetricNameMaker transactionMetricNameMaker, IPathHashMaker pathHashMaker,
            ICatHeaderHandler catHeaderHandler, IDistributedTracePayloadHandler distributedTracePayloadHandler,
            ISyntheticsHeaderHandler syntheticsHeaderHandler, ITransactionFinalizer transactionFinalizer,
            IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker,
            IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService,
            IMetricNameService metricNameService, Api.ITraceMetadataFactory traceMetadataFactory, ICATSupportabilityMetricCounters catMetricCounters)
        {
            _transactionService = transactionService;
            _transactionTransformer = transactionTransformer;
            _threadPoolStatic = threadPoolStatic;
            _transactionMetricNameMaker = transactionMetricNameMaker;
            _pathHashMaker = pathHashMaker;
            _catHeaderHandler = catHeaderHandler;
            _distributedTracePayloadHandler = distributedTracePayloadHandler;
            _syntheticsHeaderHandler = syntheticsHeaderHandler;
            _transactionFinalizer = transactionFinalizer;
            _browserMonitoringPrereqChecker = browserMonitoringPrereqChecker;
            _browserMonitoringScriptMaker = browserMonitoringScriptMaker;
            _configurationService = configurationService;
            _agentHealthReporter = agentHealthReporter;
            _agentTimerService = agentTimerService;
            _metricNameService = metricNameService;
            _traceMetadataFactory = traceMetadataFactory;
            _catMetricCounters = catMetricCounters;

            Instance = this;
        }

        public IConfiguration Configuration => _configurationService.Configuration;

        public Extensions.Logging.ILogger Logger => _logger ?? (_logger = new Logger());

        #region Transaction management

        private static void NoOpWrapperOnCreate()
        {
        }

        private ITransaction CreateTransaction(TransactionName transactionName, bool doNotTrackAsUnitOfWork, Action wrapperOnCreate)
        {
            return _transactionService.GetOrCreateInternalTransaction(transactionName, wrapperOnCreate, doNotTrackAsUnitOfWork);
        }

        public ITransaction CreateTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination, Action wrapperOnCreate)
        {
            return CreateTransaction(TransactionName.ForBrokerTransaction(destinationType, brokerVendorName, destination), true, wrapperOnCreate ?? NoOpWrapperOnCreate);
        }

        public ITransaction CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool doNotTrackAsUnitOfWork, Action wrapperOnCreate)
        {
            if (transactionDisplayName == null)
            {
                throw new ArgumentNullException("transactionDisplayName");
            }

            if (category == null)
            {
                throw new ArgumentNullException("category");
            }

            var initialTransactionName = isWeb
                ? TransactionName.ForWebTransaction(category, transactionDisplayName)
                : TransactionName.ForOtherTransaction(category, transactionDisplayName);

            return CreateTransaction(initialTransactionName, doNotTrackAsUnitOfWork, wrapperOnCreate ?? NoOpWrapperOnCreate);
        }

        public ITransaction CurrentTransaction => _transactionService.GetCurrentInternalTransaction() ?? _noOpTransaction;

        public ITraceMetadata TraceMetadata
        {
            get
            {
                if (_configurationService.Configuration.DistributedTracingEnabled && CurrentTransaction.IsValid)
                {
                    return _traceMetadataFactory.CreateTraceMetadata((IInternalTransaction)CurrentTransaction);
                }

                return Api.TraceMetadata.EmptyModel;
            }
        }

        public bool TryTrackAsyncWorkOnNewTransaction()
        {
            if (_transactionService.IsAttachedToAsyncStorage)
            {
                var currentTransaction = CurrentTransaction;

                // If the threadID under which the parent segment was created is different from the current thread
                // it means that we are on a different thread in an asychronous context.
                var currentSegment = currentTransaction.CurrentSegment as Segment;
                if (currentSegment != null && currentSegment.ThreadId != Thread.CurrentThread.ManagedThreadId)
                {
                    Detach(removeAsync: true, removePrimary: false);
                    return true;
                }
            }

            return false;
        }

        #endregion Transaction management

        #region Transaction segment managements

        public ISegment CastAsSegment(object segment)
        {
            return segment as ISegment ?? Segment.NoOpSegment;
        }

        public void EnableExplainPlans(ISegment segment, Func<object> allocateExplainPlanResources, Func<object, ExplainPlan> generateExplainPlan, Func<VendorExplainValidationResult> vendorValidateShouldExplain)
        {
            if (!_configurationService.Configuration.SqlExplainPlansEnabled || !segment.IsValid)
            {
                return;
            }

            var datastoreSegment = segment as Segment;
            var data = datastoreSegment?.Data as DatastoreSegmentData;
            if (data == null)
            {
                throw new ArgumentException("Received a datastore segment object which was not of expected type");
            }

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

        #endregion Transaction segment management

        #region inbound CAT request, outbound CAT response

        public bool TryGetDistributedTracePayloadFromHeaders<T>(IEnumerable<KeyValuePair<string, T>> headers, out T payload) where T : class
        {
            payload = null;
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (header.Key.Equals(Constants.DistributedTracePayloadKey, StringComparison.OrdinalIgnoreCase))
                    {
                        payload = header.Value;
                        return true;
                    }
                }
            }

            return false;
        }

        internal void TryProcessCatRequestData<T>(IInternalTransaction transaction, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            try
            {

                var referrerCrossApplicationProcessId = GetReferrerCrossApplicationProcessId(transaction, carrier, getter);
                if (referrerCrossApplicationProcessId == null)
                {
                    return;
                }

                UpdateReferrerCrossApplicationProcessId(transaction, referrerCrossApplicationProcessId);

                var crossApplicationRequestData = _catHeaderHandler.TryDecodeInboundRequestHeaders(carrier, getter);
                if (crossApplicationRequestData == null)
                {
                    return;
                }

                var contentLength = GetContentLength(carrier, getter);

                UpdateTransactionMetadata(transaction, crossApplicationRequestData, contentLength);

                _catMetricCounters.Record(CATSupportabilityCondition.Request_Accept_Success);
            }
            catch (Exception)
            {
                _catMetricCounters.Record(CATSupportabilityCondition.Request_Accept_Failure);
            }
        }

        internal void TryProcessSyntheticsData<T>(IInternalTransaction transaction, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            var syntheticsRequestData = _syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(carrier, getter);
            if (syntheticsRequestData == null)
            {
                return;
            }

            UpdateTransactionMetaData(transaction, syntheticsRequestData);
        }


        #endregion inbound CAT request, outboud CAT response

        #region Error handling

        public void HandleWrapperException(Exception exception)
        {
            // This method should never throw
            if (exception == null)
            {
                return;
            }

            Log.Error($"An exception occurred in a wrapper: {exception}");
        }

        #endregion Error handling

        #region Stream manipulation

        public Stream TryGetStreamInjector(Stream stream, Encoding encoding, string contentType, string requestPath)
        {
            if (stream == null)
            {
                return null;
            }

            if (encoding == null)
            {
                return null;
            }

            if (contentType == null)
            {
                return null;
            }

            if (requestPath == null)
            {
                return null;
            }

            try
            {
                var transaction = _transactionService.GetCurrentInternalTransaction();
                if (transaction == null)
                {
                    return null;
                }

                var shouldInject = _browserMonitoringPrereqChecker.ShouldAutomaticallyInject(transaction, requestPath, contentType);
                if (!shouldInject)
                {
                    return null;
                }

                // Once the transaction name is used for RUM it must be frozen
                transaction.CandidateTransactionName.Freeze(TransactionNameFreezeReason.AutoBrowserScriptInjection);
                var script = _browserMonitoringScriptMaker.GetScript(transaction);
                if (script == null)
                {
                    return null;
                }

                return new BrowserMonitoringStreamInjector(() => script, stream, encoding);
            }
            catch (Exception ex)
            {
                Log.Error($"RUM: Failed to build Browser Monitoring agent script: {ex}");
                {
                    return null;
                }
            }
        }

        #endregion Stream manipulation

        #region GetLinkingMetadata

        public Dictionary<string, string> GetLinkingMetadata()
        {
            var hostname = !string.IsNullOrEmpty(Configuration.UtilizationFullHostName)
                ? Configuration.UtilizationFullHostName
                : Configuration.UtilizationHostName;

            var metadata = new Dictionary<string, string>();
            var traceMetadata = TraceMetadata;
            if (!string.IsNullOrEmpty(traceMetadata.TraceId))
            {
                metadata.Add("trace.id", traceMetadata.TraceId);
            }

            if (!string.IsNullOrEmpty(traceMetadata.SpanId))
            {
                metadata.Add("span.id", traceMetadata.SpanId);
            }

            if (Configuration.ApplicationNames.Count() > 0)
            {
                var appName = Configuration.ApplicationNames.ElementAt(0);
                if (!string.IsNullOrEmpty(appName))
                    metadata.Add("entity.name", appName);
            }

            metadata.Add("entity.type", "SERVICE");
            if (!string.IsNullOrEmpty(Configuration.EntityGuid))
            {
                metadata.Add("entity.guid", Configuration.EntityGuid);
            }

            if (!string.IsNullOrEmpty(hostname))
            {
                metadata.Add("hostname", hostname);
            }

            return metadata;
        }

        #endregion GetLinkingMetadata

        #region ExperimentalApi

        public void RecordSupportabilityMetric(string metricName, int count)
        {
            _agentHealthReporter.ReportSupportabilityCountMetric(metricName, count);
        }

        #endregion

        #region Helpers

        private string GetReferrerCrossApplicationProcessId<T>(IInternalTransaction transaction, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            var existingReferrerProcessId = transaction.TransactionMetadata.CrossApplicationReferrerProcessId;
            if (existingReferrerProcessId != null)
            {
                _catMetricCounters.Record(CATSupportabilityCondition.Request_Accept_Multiple);
                Log.Warn($"Already received inbound cross application request with referrer cross process id: {existingReferrerProcessId}");
                return null;
            }

            return _catHeaderHandler?.TryDecodeInboundRequestHeadersForCrossProcessId(carrier, getter);
        }


        private void UpdateReferrerCrossApplicationProcessId(IInternalTransaction transaction, string referrerCrossApplicationProcessId)
        {
            transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(referrerCrossApplicationProcessId);
        }

        private void UpdateTransactionMetadata(IInternalTransaction transaction, CrossApplicationRequestData crossApplicationRequestData, long? contentLength)
        {
            if (transaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid != null)
            {
                _catMetricCounters.Record(CATSupportabilityCondition.Request_Accept_Multiple);
                //We don't return here to support legacy behavior.
            }

            if (crossApplicationRequestData.TripId != null)
            {
                transaction.TransactionMetadata.SetCrossApplicationReferrerTripId(crossApplicationRequestData.TripId);
            }

            if (contentLength != null && contentLength.Value > 0)
            {
                transaction.TransactionMetadata.SetCrossApplicationReferrerContentLength(contentLength.Value);
            }

            if (crossApplicationRequestData.PathHash != null)
            {
                transaction.TransactionMetadata.SetCrossApplicationReferrerPathHash(crossApplicationRequestData.PathHash);
            }

            if (crossApplicationRequestData.TransactionGuid != null)
            {
                transaction.TransactionMetadata.SetCrossApplicationReferrerTransactionGuid(crossApplicationRequestData.TransactionGuid);
            }
        }

        private long GetContentLength<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            long contentLength = default;
            var headers = getter(carrier, "Content-Length");
            if (headers?.Count() > 0)
            {
                var contentLengthString = headers.FirstOrDefault();
                long.TryParse(contentLengthString, out contentLength);
            }

            return contentLength;
        }

        private void UpdateTransactionMetaData(IInternalTransaction transaction, SyntheticsHeader syntheticsHeader)
        {
            transaction.TransactionMetadata.SetSyntheticsResourceId(syntheticsHeader.ResourceId);
            transaction.TransactionMetadata.SetSyntheticsJobId(syntheticsHeader.JobId);
            transaction.TransactionMetadata.SetSyntheticsMonitorId(syntheticsHeader.MonitorId);
        }

        internal void Detach(bool removeAsync, bool removePrimary)
        {
            _transactionService.RemoveOutstandingInternalTransactions(removeAsync, removePrimary);
        }

        #endregion
    }
}
