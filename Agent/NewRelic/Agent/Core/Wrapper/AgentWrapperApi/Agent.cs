using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
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
using System.Text;
using System.Threading;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	public class Agent : IAgent // any changes to api, update the interface in extensions and re-import, then implement in legacy api as NotImplementedException
	{
		public const int QueryParameterMaxStringLength = 256;

		internal readonly ITransactionService _transactionService;
		internal readonly ITimerFactory _timerFactory;
		internal readonly ITransactionTransformer _transactionTransformer;
		internal readonly IThreadPoolStatic _threadPoolStatic;
		internal readonly ITransactionMetricNameMaker _transactionMetricNameMaker;
		internal readonly IPathHashMaker _pathHashMaker;
		internal readonly ICatHeaderHandler _catHeaderHandler;
		internal readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;
		internal readonly ISyntheticsHeaderHandler _syntheticsHeaderHandler;
		internal readonly ITransactionFinalizer _transactionFinalizer;
		internal readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
		internal readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
		internal readonly IConfigurationService _configurationService;
		internal readonly IAgentHealthReporter _agentHealthReporter;
		internal readonly IAgentTimerService _agentTimerService;
		internal readonly IMetricNameService _metricNameService;
		internal Extensions.Logging.ILogger _logger;

		internal static Agent Instance;
		private static readonly ITransaction NoOpTransaction = new NoOpTransaction();

		public Agent(ITransactionService transactionService, ITimerFactory timerFactory, ITransactionTransformer transactionTransformer, IThreadPoolStatic threadPoolStatic, ITransactionMetricNameMaker transactionMetricNameMaker, IPathHashMaker pathHashMaker, ICatHeaderHandler catHeaderHandler, IDistributedTracePayloadHandler distributedTracePayloadHandler, ISyntheticsHeaderHandler syntheticsHeaderHandler, ITransactionFinalizer transactionFinalizer, IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService, IMetricNameService metricNameService)
		{
			_transactionService = transactionService;
			_timerFactory = timerFactory;
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

			Instance = this;
		}

		public IConfiguration Configuration => _configurationService.Configuration;

		public Extensions.Logging.ILogger Logger => _logger ?? (_logger = new Logger());

		#region Transaction management

		private static void _noOpWrapperOnCreate() { }


		private ITransaction CreateTransaction(TransactionName transactionName, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			return _transactionService.GetOrCreateInternalTransaction(transactionName, wrapperOnCreate ?? _noOpWrapperOnCreate, mustBeRootTransaction);
		}

		public ITransaction CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool mustBeRootTransaction)
		{
			#pragma warning disable CS0618 // Type or member is obsolete
			return CreateTransaction(isWeb, category, transactionDisplayName, mustBeRootTransaction, _noOpWrapperOnCreate);
			#pragma warning restore CS0618 // Type or member is obsolete
		}

		private ITransaction CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool mustBeRootTransaction, Action wrapperOnCreate)
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

			return CreateTransaction(initialTransactionName, mustBeRootTransaction, wrapperOnCreate);
		}
		
		[Obsolete("Use AgentWrapperAPI.CreateTransaction method instead")]
		public ITransaction CreateWebTransaction(WebTransactionType type, string transactionDisplayName, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			return CreateTransaction(TransactionName.ForWebTransaction(type, transactionDisplayName), mustBeRootTransaction, wrapperOnCreate);
		}

		[Obsolete("Use AgentWrapperAPI.CreateTransaction method instead")]
		public ITransaction CreateMessageBrokerTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination, Action wrapperOnCreate)
		{
			return CreateTransaction(TransactionName.ForBrokerTransaction(destinationType, brokerVendorName, destination), true, wrapperOnCreate);
		}

		[Obsolete("Use AgentWrapperAPI.CreateTransaction method instead")]
		public ITransaction CreateOtherTransaction(string category, string name, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			return CreateTransaction(false, category, name, mustBeRootTransaction, wrapperOnCreate);
		}

		public ITransaction CurrentTransaction => _transactionService.GetCurrentInternalTransaction() ?? NoOpTransaction;

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

		#endregion Transaction segment management

		#region inbound CAT request, outbound CAT response

		public void ProcessInboundRequest(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType, long? contentLength)
		{
			var transaction = _transactionService.GetCurrentInternalTransaction();
			if (transaction == null)
			{
				return;
			}

			if (_configurationService.Configuration.DistributedTracingEnabled)
			{
				if (TryGetDistributedTracePayloadFromHeaders(headers, out var payload))
				{
					CurrentTransaction.AcceptDistributedTracePayload(payload, transportType);
				}
			}
			else
			{
				// NOTE: the key for this dictionary should NOT be case sensitive since HTTP headers are not case sensitive.
				// See: https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
				var headerDictionary = headers.ToDictionary(equalityComparer: StringComparer.OrdinalIgnoreCase);
				TryProcessCatRequestData(transaction, headerDictionary, contentLength);
				TryProcessSyntheticsData(transaction, headerDictionary);
			}
		}

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

		private void TryProcessCatRequestData(IInternalTransaction transaction, IDictionary<string, string> headers, long? contentLength)
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

		private void TryProcessSyntheticsData(IInternalTransaction transaction, IDictionary<string, string> headers)
		{
			var syntheticsRequestData = _syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(headers);
			if (syntheticsRequestData == null)
				return;

			UpdateTransactionMetaData(transaction, syntheticsRequestData);
		}


		#endregion inbound CAT request, outboud CAT response

		#region Error handling

		public void HandleWrapperException(Exception exception)
		{
			// This method should never throw
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			// ReSharper disable once HeuristicUnreachableCode
			if (exception == null)
				return;

			Log.Error($"An exception occurred in a wrapper: {exception}");
		}

		#endregion Error handling

		#region Stream manipulation

		public Stream TryGetStreamInjector(Stream stream, Encoding encoding, string contentType, string requestPath)
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
				transaction.CandidateTransactionName.Freeze(TransactionNameFreezeReason.AutoBrowserScriptInjection);

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

		private string GetReferrerCrossApplicationProcessId(IInternalTransaction transaction, IDictionary<string, string> headers)
		{
			var existingReferrerProcessId = transaction.TransactionMetadata.CrossApplicationReferrerProcessId;
			if (existingReferrerProcessId != null)
			{
				Log.Warn($"Already received inbound cross application request with referrer cross process id: {existingReferrerProcessId}");
				return null;
			}

			return _catHeaderHandler?.TryDecodeInboundRequestHeadersForCrossProcessId(headers);
		}


		private void UpdateReferrerCrossApplicationProcessId(IInternalTransaction transaction, string referrerCrossApplicationProcessId)
		{
			transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(referrerCrossApplicationProcessId);
		}

		private void UpdateTransactionMetadata(IInternalTransaction transaction, CrossApplicationRequestData crossApplicationRequestData, long? contentLength)
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
