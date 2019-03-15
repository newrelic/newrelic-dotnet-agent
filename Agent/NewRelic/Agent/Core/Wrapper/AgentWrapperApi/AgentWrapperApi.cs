using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NewRelic.SystemExtensions.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	public class AgentWrapperApi : IAgentWrapperApi // any changes to api, update the interface in extensions and re-import, then implement in legacy api as NotImplementedException
	{
		internal static readonly int MaxSegmentLength = 255;
		public const int QueryParameterMaxStringLength = 256;

		private readonly ITransactionService _transactionService;

		private readonly ITimerFactory _timerFactory;

		private readonly ITransactionTransformer _transactionTransformer;

		private readonly IThreadPoolStatic _threadPoolStatic;

		private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

		private readonly IPathHashMaker _pathHashMaker;

		private readonly ICatHeaderHandler _catHeaderHandler;

		private readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;

		private readonly ISyntheticsHeaderHandler _syntheticsHeaderHandler;

		private readonly ITransactionFinalizer _transactionFinalizer;

		private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;

		private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		private readonly IConfigurationService _configurationService;

		private readonly IAgentHealthReporter _agentHealthReporter;

		private readonly IAgentTimerService _agentTimerService;

		private readonly IMetricNameService _metricNameService;

		private ILogger _logger;

		private static readonly ITransactionWrapperApi NoOpTransactionWrapperApi = new NoTransactionWrapperApiImpl();

		private static readonly ISegment _noOpSegment = new NoTransactionWrapperApiImpl();

		public AgentWrapperApi(ITransactionService transactionService, ITimerFactory timerFactory, ITransactionTransformer transactionTransformer, IThreadPoolStatic threadPoolStatic, ITransactionMetricNameMaker transactionMetricNameMaker, IPathHashMaker pathHashMaker, ICatHeaderHandler catHeaderHandler, IDistributedTracePayloadHandler distributedTracePayloadHandler, ISyntheticsHeaderHandler syntheticsHeaderHandler, ITransactionFinalizer transactionFinalizer, IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService, IMetricNameService metricNameService)
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
		}

		public IConfiguration Configuration => _configurationService.Configuration;

		public ILogger Logger => _logger ?? (_logger = new Logger());

		#region Transaction management

		private static void _noOpWrapperOnCreate() { }


		private ITransactionWrapperApi CreateTransaction(TransactionName transactionName, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			var transaction = new TransactionWrapperApi(this, _transactionService.GetOrCreateInternalTransaction(transactionName, wrapperOnCreate ?? _noOpWrapperOnCreate, mustBeRootTransaction));

			return transaction;
		}

		public ITransactionWrapperApi CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool mustBeRootTransaction)
		{
			#pragma warning disable CS0618 // Type or member is obsolete
			return CreateTransaction(isWeb, category, transactionDisplayName, mustBeRootTransaction, _noOpWrapperOnCreate);
			#pragma warning restore CS0618 // Type or member is obsolete
		}

		private ITransactionWrapperApi CreateTransaction(bool isWeb, string category, string transactionDisplayName, bool mustBeRootTransaction, Action wrapperOnCreate)
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
		public ITransactionWrapperApi CreateWebTransaction(WebTransactionType type, string transactionDisplayName, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			return CreateTransaction(TransactionName.ForWebTransaction(type, transactionDisplayName), mustBeRootTransaction, wrapperOnCreate);
		}

		[Obsolete("Use AgentWrapperAPI.CreateTransaction method instead")]
		public ITransactionWrapperApi CreateMessageBrokerTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination, Action wrapperOnCreate)
		{
			return CreateTransaction(TransactionName.ForBrokerTransaction(destinationType, brokerVendorName, destination), true, wrapperOnCreate);
		}

		[Obsolete("Use AgentWrapperAPI.CreateTransaction method instead")]
		public ITransactionWrapperApi CreateOtherTransaction(string category, string name, bool mustBeRootTransaction, Action wrapperOnCreate)
		{
			return CreateTransaction(false, category, name, mustBeRootTransaction, wrapperOnCreate);
		}

		public ITransactionWrapperApi CurrentTransactionWrapperApi
		{
			get
			{
				var transaction = _transactionService.GetCurrentInternalTransaction();
				return transaction == null ? NoOpTransactionWrapperApi : new TransactionWrapperApi(this, transaction);
			}
		}

		public void EndTransaction(ITransaction transaction, bool captureResponseTime)
		{
			if (captureResponseTime)
			{
				//This only does something once so it's safe to perform each time EndTransaction is called
				var previouslyCapturedResponseTime = !transaction.TryCaptureResponseTime();
				if (previouslyCapturedResponseTime && Log.IsDebugEnabled)
				{
					var stackTrace = new StackTrace();
					Log.DebugFormat("Transaction has already captured the response time.{0}{1}", System.Environment.NewLine, stackTrace);
				}
			}

			var remainingUnitsOfWork = transaction.NoticeUnitOfWorkEnds();

			// There is still work to do, so delay ending transaction until there no more work left
			if (remainingUnitsOfWork > 0)
			{
				return;
			}

			// We want to finish then transaction as fast as possible to get the more accurate possible response time
			var finishedTransaction = _transactionFinalizer.Finish(transaction);

			if (!finishedTransaction) return;

			// We also want to remove the transaction from the transaction context before returning so that it won't be reused
			_transactionService.RemoveOutstandingInternalTransactions(true, true);

			var timer = _agentTimerService.StartNew("TransformDelay");
			Action transformWork = () =>
			{
				timer?.StopAndRecordMetric();
				_transactionTransformer.Transform(transaction);
			};

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

		public bool TryTrackAsyncWorkOnNewTransaction()
		{
			if (_transactionService.IsAttachedToAsyncStorage)
			{
				var currentTransaction = CurrentTransactionWrapperApi;

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

		private IEnumerable<KeyValuePair<string, string>> GetOutboundRequestHeaders(ITransaction transaction)
		{
			var headers = Enumerable.Empty<KeyValuePair<string, string>>();

			var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;
			var currentTransactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

			UpdatePathHash(transaction, currentTransactionMetricName);

			return headers
				.Concat(_catHeaderHandler.TryGetOutboundRequestHeaders(transaction))
				.Concat(_syntheticsHeaderHandler.TryGetOutboundSyntheticsRequestHeader(transaction));
		}

		#endregion outbound CAT request, inbound CAT response

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
					CurrentTransactionWrapperApi.AcceptDistributedTracePayload(payload, transportType);
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

		private void TryProcessCatRequestData(ITransaction transaction, IDictionary<string, string> headers, long? contentLength)
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

		private void TryProcessSyntheticsData(ITransaction transaction, IDictionary<string, string> headers)
		{
			var syntheticsRequestData = _syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(headers);
			if (syntheticsRequestData == null)
				return;

			UpdateTransactionMetaData(transaction, syntheticsRequestData);
		}

		private IEnumerable<KeyValuePair<string, string>> GetOutboundResponseHeaders(ITransaction transaction)
		{
			var headers = Enumerable.Empty<KeyValuePair<string, string>>();
			
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
			transaction.TransactionMetadata.SetCrossApplicationResponseTimeInSeconds((float)transaction.GetDurationUntilNow().TotalSeconds);

			//We should only add outbound CAT headers here (not synthetics) given that this is a Response to a request
			return headers
				.Concat(_catHeaderHandler.TryGetOutboundResponseHeaders(transaction, currentTransactionMetricName));
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

		private string GetReferrerCrossApplicationProcessId(ITransaction transaction, IDictionary<string, string> headers)
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

		private void UpdateReferrerCrossApplicationProcessId(ITransaction transaction, string referrerCrossApplicationProcessId)
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
			var isAdded =_transactionService.SetTransactionOnAsyncContext(transaction);
			if(!isAdded)
			{
				Log.Debug("Failed to add transaction to async storage. This can occur when there are no async context providers.");
			}
		}

		private void Detach(bool removeAsync, bool removePrimary)
		{
			_transactionService.RemoveOutstandingInternalTransactions(removeAsync, removePrimary);
		}

		#endregion

		/// <summary>
		/// This is a simple shim.  It cannot keep any state.  We could potentially remove the
		/// need for this class by implementing the extension ITransaction interface in the agent's
		/// transaction implementation.
		/// </summary>
		private sealed class TransactionWrapperApi : ITransactionWrapperApi
		{
			private readonly AgentWrapperApi agentWrapperApi;
			private readonly ITransaction transaction;

			public bool IsValid => true;

			public bool IsFinished => transaction.IsFinished;

			public ISegment CurrentSegment
			{
				get
				{
					var currentSegmentIndex = transaction.CallStackManager.TryPeek();
					if (!currentSegmentIndex.HasValue)
					{
						return _noOpSegment;
					}

					return transaction.Segments[currentSegmentIndex.Value];
				}
			}

			public TransactionWrapperApi(AgentWrapperApi agentWrapperApi, ITransaction transaction)
			{
				this.agentWrapperApi = agentWrapperApi;
				this.transaction = transaction;
			}

			public void End(bool captureResponseTime = true)
			{
				if (transaction.IsFinished)
				{
					Log.DebugFormat("Transaction {0} has already been ended.", transaction.Guid);
					return;
				}

				if (transaction.CandidateTransactionName.CurrentTransactionName.IsWeb && transaction.TransactionMetadata.HttpResponseStatusCode >= 400)
				{
					SetTransactionName(TransactionName.ForWebTransaction(WebTransactionType.StatusCode, transaction.TransactionMetadata.HttpResponseStatusCode.ToString()), TransactionNamePriority.StatusCode);

				}

				agentWrapperApi.EndTransaction(transaction, captureResponseTime);
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

				//TODO: Since the CustomSegmentBuilder is the only thing that separates this from the
				//other segments, there is an opportunity to refactor here.
				return new TypedSegment<CustomSegmentData>(transaction.GetTransactionSegmentState(), methodCallData, new CustomSegmentData(segmentName));
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

				return new TypedSegment<MessageBrokerSegmentData>(transaction.GetTransactionSegmentState(), methodCallData,
					new MessageBrokerSegmentData(brokerVendorName, destinationName, destType, action));
			}

			public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
			{
				if (transaction.Ignored)
					return _noOpSegment;

				if (!agentWrapperApi._configurationService.Configuration.DatastoreTracerQueryParametersEnabled)
				{
					queryParameters = null;
				}

				if (parsedSqlStatement == null)
				{
					Log.Error("StartDatastoreSegment - parsedSqlStatement is null. The parsedSqlStatement should never be null. This indicates that the instrumentation was unable to parse a datastore statement.");
					parsedSqlStatement = new ParsedSqlStatement(DatastoreVendor.Other, null, null);
				}

				var data = new DatastoreSegmentData(parsedSqlStatement, commandText, connectionInfo, GetNormalizedQueryParameters(queryParameters));
				var segment = new TypedSegment<DatastoreSegmentData>(transaction.GetTransactionSegmentState(), GetMethodCallData(methodCall), data);
				segment.IsLeaf = isLeaf;
				return segment;
			}

			private Dictionary<string, IConvertible> GetNormalizedQueryParameters(IDictionary<string, IConvertible> originalQueryParameters)
			{
				if (originalQueryParameters == null)
				{
					return null;
				}

				var normalizedQueryParameters = new Dictionary<string, IConvertible>(originalQueryParameters.Count);

				foreach (var queryParameter in originalQueryParameters)
				{
					try
					{
						if (queryParameter.Value == null)
						{
							continue;
						}

						var normalizedKey = GetTruncatedString(queryParameter.Key);

						normalizedQueryParameters[normalizedKey] = GetNormalizedValue(queryParameter.Value);
					}
					catch (Exception e)
					{
						Log.DebugFormat("Error while normalizing query parameters: {0}", e);
					}
				}

				return normalizedQueryParameters;
			}

			private string GetTruncatedString(string originalString)
			{
				if (originalString.Length <= QueryParameterMaxStringLength)
				{
					return originalString;
				}

				return originalString.Substring(0, QueryParameterMaxStringLength);
			}

			private IConvertible GetNormalizedValue(IConvertible originalValue)
			{
				switch (Type.GetTypeCode(originalValue.GetType()))
				{
					case TypeCode.Byte:
					case TypeCode.SByte:
					case TypeCode.UInt16:
					case TypeCode.UInt32:
					case TypeCode.UInt64:
					case TypeCode.Int16:
					case TypeCode.Int32:
					case TypeCode.Int64:
					case TypeCode.Decimal:
					case TypeCode.Double:
					case TypeCode.Single:
					case TypeCode.Boolean:
					case TypeCode.Char:
						return originalValue;
					case TypeCode.String:
						return GetTruncatedString(originalValue as string);
					default:
						var valueAsString = originalValue.ToString(CultureInfo.InvariantCulture);
						return GetTruncatedString(valueAsString);
				}
			}

			public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf = false)
			{
				if (transaction.Ignored)
					return _noOpSegment;
				if (destinationUri == null)
					throw new ArgumentNullException(nameof(destinationUri));
				if (method == null)
					throw new ArgumentNullException(nameof(method));
				if (!destinationUri.IsAbsoluteUri)
					throw new ArgumentException("Must use an absolute URI, not a relative URI", nameof(destinationUri));

				var methodCallData = GetMethodCallData(methodCall);
				var segment = new TypedSegment<ExternalSegmentData>(transaction.GetTransactionSegmentState(), methodCallData,
					new ExternalSegmentData(destinationUri, method));
				segment.IsExternal = true;
				segment.IsLeaf = isLeaf;
				return segment;
				// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
			}

			public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false)
			{
				if (transaction.Ignored)
					return _noOpSegment;
				if (typeName == null)
					throw new ArgumentNullException(nameof(typeName));
				if (methodName == null)
					throw new ArgumentNullException(nameof(methodName));

				var methodCallData = GetMethodCallData(methodCall);

				var segment = new TypedSegment<MethodSegmentData>(transaction.GetTransactionSegmentState(), methodCallData, new MethodSegmentData(typeName, methodName));
				segment.IsLeaf = isLeaf;
				return segment;

				// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
			}

			public ISegment StartTransactionSegment(MethodCall methodCall, string segmentDisplayName)
			{
				if (transaction.Ignored)
					return _noOpSegment;
				if (segmentDisplayName == null)
					throw new ArgumentNullException("segmentDisplayName");

				var methodCallData = GetMethodCallData(methodCall);
				return new TypedSegment<SimpleSegmentData>(transaction.GetTransactionSegmentState(), methodCallData, new SimpleSegmentData(segmentDisplayName));

				// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
			}

			public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
			{
				if (agentWrapperApi._configurationService.Configuration.DistributedTracingEnabled)
				{
					var payload = CreateDistributedTracePayload();
					if (payload.IsEmpty())
					{
						return Enumerable.Empty<KeyValuePair<string, string>>();
					}
					return new[] { new KeyValuePair<string, string>(Constants.DistributedTracePayloadKey, payload.HttpSafe()) };
				}

				return agentWrapperApi.GetOutboundRequestHeaders(transaction);
			}

			public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
			{
				return agentWrapperApi.GetOutboundResponseHeaders(transaction);
			}

			public void AcceptDistributedTracePayload(string payload, TransportType transportType)
			{
				if (!agentWrapperApi._configurationService.Configuration.DistributedTracingEnabled)
				{
					return;
				}
				if (transaction.TransactionMetadata.HasOutgoingDistributedTracePayload)
				{
					agentWrapperApi._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept();
					return;
				}
				if (transaction.TransactionMetadata.HasIncomingDistributedTracePayload)
				{
					agentWrapperApi._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple();
					return;
				}

				TryProcessDistributedTraceRequestData(payload, transportType);
			}

			public IDistributedTraceApiModel CreateDistributedTracePayload()
			{
				if (!agentWrapperApi._configurationService.Configuration.DistributedTracingEnabled)
				{
					return DistributedTraceApiModel.EmptyModel;
				}

				return agentWrapperApi._distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(transaction, CurrentSegment);
			}

			private void TryProcessDistributedTraceRequestData(string payload, TransportType transportType)
			{
				var distributedTracePayload = agentWrapperApi._distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(payload);

				if (distributedTracePayload == null)
					return;

				UpdateTransactionMetadata(distributedTracePayload, transportType);
			}

			private void UpdateTransactionMetadata(DistributedTracePayload distributedTracePayload, TransportType transportType)
			{
				transaction.TransactionMetadata.HasIncomingDistributedTracePayload = true;
				transaction.TransactionMetadata.DistributedTraceType = distributedTracePayload.Type;
				transaction.TransactionMetadata.DistributedTraceAccountId = distributedTracePayload.AccountId;
				transaction.TransactionMetadata.DistributedTraceAppId = distributedTracePayload.AppId;
				transaction.TransactionMetadata.DistributedTraceGuid = distributedTracePayload.Guid;
				transaction.TransactionMetadata.SetDistributedTraceTransportType(transportType);
				transaction.TransactionMetadata.DistributedTraceTransportDuration = ComputeDuration(transaction.StartTime, distributedTracePayload.Timestamp);
				transaction.TransactionMetadata.DistributedTraceTraceId = distributedTracePayload.TraceId;
				transaction.TransactionMetadata.DistributedTraceTrustKey = distributedTracePayload.TrustKey;
				transaction.TransactionMetadata.DistributedTraceTransactionId = distributedTracePayload.TransactionId;

				if (distributedTracePayload.Priority.HasValue)
				{
					transaction.TransactionMetadata.Priority = distributedTracePayload.Priority.Value;
				}
				transaction.TransactionMetadata.DistributedTraceSampled = distributedTracePayload.Sampled;
			}

			private TimeSpan ComputeDuration(DateTime transactionStart, DateTime payloadStart)
			{
				var duration = transactionStart - payloadStart;
				return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
			}

			public void NoticeError(Exception exception)
			{
				Log.Debug($"Noticed application error: {exception}");

				// CHeck whether this exception is being ignored
				// to avoid stacktrace lookup etc. 
				var exceptionTypeName = exception.GetType().FullName;
				if (!ErrorData.ShouldIgnoreError(exceptionTypeName, agentWrapperApi._configurationService))
				{
					var errorData = ErrorData.FromException(exception,
						agentWrapperApi._configurationService.Configuration.StripExceptionMessages);

					transaction.TransactionMetadata.AddExceptionData(errorData);
				}
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
				agentWrapperApi.Detach(true, true);
			}

			public void DetachFromPrimary()
			{
				agentWrapperApi.Detach(false, true);
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
				End(captureResponseTime: false);
			}

			private void SetTransactionName(ITransactionName transactionName, TransactionNamePriority priority)
			{
				transaction.CandidateTransactionName.TrySet(transactionName, priority);
			}
			
			public void SetWebTransactionName(WebTransactionType type, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{
				var trxName = TransactionName.ForWebTransaction(type, name);
				SetTransactionName(trxName, priority);
			}

			public void SetWebTransactionNameFromPath(string normalizedPath)
			{
				var trxName = TransactionName.ForUriTransaction(normalizedPath);
				SetTransactionName(trxName, TransactionNamePriority.Uri);
			}

			public void SetWebTransactionNameFromPath(WebTransactionType type, string path)
			{
				var cleanedPath = UriHelpers.GetTransactionNameFromPath(path);
				var normalizedPath = agentWrapperApi._metricNameService.NormalizeUrl(cleanedPath);

				var trxName = TransactionName.ForUriTransaction(normalizedPath);

				SetTransactionName(trxName, TransactionNamePriority.Uri);
			}


			public void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{
				var trxName = TransactionName.ForBrokerTransaction(destinationType, brokerVendorName, destination);
				SetTransactionName(trxName, priority);
			}

			public void SetOtherTransactionName(string category, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{
				var trxName = TransactionName.ForOtherTransaction(category, name);
				SetTransactionName(trxName, priority);
			}

			public void SetCustomTransactionName(string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{
				var trxName = TransactionName.ForCustomTransaction(transaction.CandidateTransactionName.CurrentTransactionName.IsWeb, name, MaxSegmentLength);
				SetTransactionName(trxName, priority);
			}

			public void SetUri(string uri)
			{
				if (uri == null)
				{ 
					throw new ArgumentNullException(nameof(uri));
				}

				var cleanUri = StringsHelper.CleanUri(uri);

				transaction.TransactionMetadata.SetUri(cleanUri);
			}

			public void SetOriginalUri(string uri)
			{
				if (uri == null)
				{ 
					throw new ArgumentNullException(nameof(uri));
				}

				var cleanUri = StringsHelper.CleanUri(uri);

				transaction.TransactionMetadata.SetOriginalUri(cleanUri);
			}

			public void SetReferrerUri(string uri)
			{
				if (uri == null)
				{ 
					throw new ArgumentNullException(nameof(uri));
				}

				// Always strip query string parameters on a referrer. Per https://newrelic.atlassian.net/browse/DOTNET-2141
				var cleanUri = StringsHelper.CleanUri(uri);

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

			public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters)
			{
				if (parameters == null)
				{ 
					throw new ArgumentNullException(nameof(parameters));
				}

				foreach(var parameter in parameters)
				{
					if ( parameter.Key != null && parameter.Value != null)
					{
						transaction.TransactionMetadata.AddRequestParameter(parameter.Key, parameter.Value);
					}
				}
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

			public ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor vendor, CommandType commandType, string sql)
			{
				return transaction.GetParsedDatabaseStatement(vendor, commandType, sql);
			}
		}

		private sealed class NoTransactionWrapperApiImpl : ITransactionWrapperApi, ISegment
		{
			public bool IsValid => false;

			public bool DurationShouldBeDeductedFromParent { get; set; } = false;

			public bool IsFinished => false;

			public ISegment CurrentSegment => _noOpSegment;

			public bool IsLeaf => false;

			public bool IsExternal => false;

			public string SpanId => null;

			public void End()
			{
			}

			public void End(bool captureResponseTime = true)
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

			public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
			{
#if DEBUG
				Log.Finest("Skipping StartDatastoreSegment outside of a transaction");
#endif
				return this;
			}

			public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf = false)
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

			public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false)
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

			public void AcceptDistributedTracePayload(string payload, TransportType transportType)
			{
				Log.Debug("Tried to accept distributed trace payload, but there was no transaction");
			}

			public IDistributedTraceApiModel CreateDistributedTracePayload()
			{
				Log.Debug("Tried to create distributed trace payload, but there was no transaction");

				return DistributedTraceApiModel.EmptyModel;
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

			public void SetWebTransactionName(WebTransactionType type, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{

			}

			public void SetWebTransactionNameFromPath(WebTransactionType type, string path)
			{

			}

			public void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{

			}

			public void SetOtherTransactionName(string category, string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
			{

			}

			public void SetCustomTransactionName(string name, TransactionNamePriority priority = TransactionNamePriority.Uri)
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

			public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters)
			{

			}

			public object GetOrSetValueFromCache(string key, Func<object> func)
			{
				return null;
			}

			public void Ignore()
			{
			}

			public ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor vendor, CommandType commandType, string sql)
			{
				return null;
			}
		}
	}
}
