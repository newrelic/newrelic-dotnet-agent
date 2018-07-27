using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.DistributedTracing;
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
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	public class AgentWrapperApi : IAgentWrapperApi // any changes to api, update the interface in extensions and re-import, then implement in legacy api as NotImplementedException
	{
		internal static readonly int MaxSegmentLength = 255;
		public const int QueryParameterMaxStringLength = 256;

		[NotNull]
		private readonly ITransactionService _transactionService;

		[NotNull]
		private readonly ITimerFactory _timerFactory;

		[NotNull]
		private readonly ITransactionTransformer _transactionTransformer;

		[NotNull]
		private readonly IThreadPoolStatic _threadPoolStatic;

		[NotNull]
		private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private readonly IPathHashMaker _pathHashMaker;

		[NotNull]
		private readonly ICatHeaderHandler _catHeaderHandler;

		[NotNull]
		private readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;

		[NotNull]
		private readonly ISyntheticsHeaderHandler _syntheticsHeaderHandler;

		[NotNull]
		private readonly ITransactionFinalizer _transactionFinalizer;

		[NotNull]
		private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;

		[NotNull]
		private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;

		[NotNull]
		private readonly IConfigurationService _configurationService;

		private readonly IAgentHealthReporter _agentHealthReporter;

		private readonly IAgentTimerService _agentTimerService;

		[NotNull]
		private static readonly Extensions.Providers.Wrapper.ITransaction _noOpTransaction = new NoTransactionImpl();

		[NotNull]
		private static readonly ISegment _noOpSegment = new NoTransactionImpl();

		public AgentWrapperApi([NotNull] ITransactionService transactionService, [NotNull] ITimerFactory timerFactory, [NotNull] ITransactionTransformer transactionTransformer, [NotNull] IThreadPoolStatic threadPoolStatic, [NotNull] ITransactionMetricNameMaker transactionMetricNameMaker, [NotNull] IPathHashMaker pathHashMaker, [NotNull] ICatHeaderHandler catHeaderHandler, [NotNull] IDistributedTracePayloadHandler distributedTracePayloadHandler, [NotNull] ISyntheticsHeaderHandler syntheticsHeaderHandler, [NotNull] ITransactionFinalizer transactionFinalizer, [NotNull] IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, [NotNull] IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAgentTimerService agentTimerService)
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
		}

		public IConfiguration Configuration => _configurationService.Configuration;

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

		public Extensions.Providers.Wrapper.ITransaction CreateMessageBrokerTransaction(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination, Action wrapperOnCreate)
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
			var remainingUnitsOfWork = transaction.NoticeUnitOfWorkEnds();

			// There is still work to do, so delay ending transaction until there no more work left
			if (remainingUnitsOfWork > 0)
			{
				return;
			}

			// We want to finish then transaction as fast as possible to get the more accurate possible response time
			_transactionFinalizer.Finish(transaction);

			// We also want to remove the transaction from the transaction context before returning so that it won't be reused
			_transactionService.RemoveOutstandingInternalTransactions(true);

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

		#endregion Transaction management

		#region Transaction Mutation
		
		[NotNull]
		private static string GetTransactionName([NotNull] string path)
		{
			if (path.StartsWith("/"))
				path = path.Substring(1);

			if (path == string.Empty)
				path = "Root";

			return path;
		}
		
		#endregion Transaction Mutation

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

		private IEnumerable<KeyValuePair<string, string>> GetOutboundRequestHeaders([NotNull] ITransaction transaction)
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

		public void ProcessInboundRequest(IEnumerable<KeyValuePair<string, string>> headers, [CanBeNull] string transportType, long? contentLength)
		{
			var transaction = _transactionService.GetCurrentInternalTransaction();
			if (transaction == null)
				return;

			if (_configurationService.Configuration.DistributedTracingEnabled)
			{
				CurrentTransaction.AcceptDistributedTracePayload(headers, transportType);
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

		private void TryProcessCatRequestData([NotNull] ITransaction transaction, [NotNull] IDictionary<string, string> headers, long? contentLength)
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

		private void TryProcessSyntheticsData([NotNull] ITransaction transaction, [NotNull] IDictionary<string, string> headers)
		{
			var syntheticsRequestData = _syntheticsHeaderHandler.TryDecodeInboundRequestHeaders(headers);
			if (syntheticsRequestData == null)
				return;

			UpdateTransactionMetaData(transaction, syntheticsRequestData);
		}

		private IEnumerable<KeyValuePair<string, string>> GetOutboundResponseHeaders([NotNull] ITransaction transaction)
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

		private void ExecuteOffRequestThread([NotNull] Action action)
		{
			_threadPoolStatic.QueueUserWorkItem(_ => action.CatchAndLog());
		}

		private string GetReferrerCrossApplicationProcessId([NotNull] ITransaction transaction, [NotNull] IDictionary<string, string> headers)
		{
			var existingReferrerProcessId = transaction.TransactionMetadata.CrossApplicationReferrerProcessId;
			if (existingReferrerProcessId != null)
			{
				Log.Warn($"Already received inbound cross application request with referrer cross process id: {existingReferrerProcessId}");
				return null;
			}

			return _catHeaderHandler?.TryDecodeInboundRequestHeadersForCrossProcessId(headers);
		}

		private void UpdatePathHash([NotNull] ITransaction transaction, TransactionMetricName transactionMetricName)
		{
			var pathHash = _pathHashMaker.CalculatePathHash(transactionMetricName.PrefixedName, transaction.TransactionMetadata.CrossApplicationReferrerPathHash);
			transaction.TransactionMetadata.SetCrossApplicationPathHash(pathHash);
		}

		private void UpdateReferrerCrossApplicationProcessId([NotNull] ITransaction transaction, [NotNull] string referrerCrossApplicationProcessId)
		{
			transaction.TransactionMetadata.SetCrossApplicationReferrerProcessId(referrerCrossApplicationProcessId);
		}

		private void UpdateTransactionMetadata([NotNull] ITransaction transaction, [NotNull] CrossApplicationRequestData crossApplicationRequestData, long? contentLength)
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

		private void UpdateTransactionMetaData([NotNull] ITransaction transaction, [NotNull] SyntheticsHeader syntheticsHeader)
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

			public ISegment ParentSegment
			{
				get
				{
					var parentSegmentIndex = transaction.CallStackManager.TryPeek();
					if (!parentSegmentIndex.HasValue)
					{
						return _noOpSegment;
					}

					return transaction.Segments[parentSegmentIndex.Value];
				}
			}

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

			public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method)
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
				return new TypedSegment<ExternalSegmentData>(transaction.GetTransactionSegmentState(), methodCallData,
					new ExternalSegmentData(destinationUri, method));

				// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
			}

			public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName)
			{
				if (transaction.Ignored)
					return _noOpSegment;
				if (typeName == null)
					throw new ArgumentNullException(nameof(typeName));
				if (methodName == null)
					throw new ArgumentNullException(nameof(methodName));
				
				var methodCallData = GetMethodCallData(methodCall);
				return new TypedSegment<MethodSegmentData>(transaction.GetTransactionSegmentState(), methodCallData, new MethodSegmentData(typeName, methodName));

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

			public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata(ISegment segment = null)
			{
				if (agentWrapperApi._configurationService.Configuration.DistributedTracingEnabled)
				{
					return CreateDistributedTracePayload(segment);
				}

				return agentWrapperApi.GetOutboundRequestHeaders(transaction);
			}

			public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
			{
				return agentWrapperApi.GetOutboundResponseHeaders(transaction);
			}

			public void AcceptDistributedTracePayload(IEnumerable<KeyValuePair<string, string>> headers, [CanBeNull] string transportType)
			{
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

				TryProcessDistributedTraceRequestData(headers, transportType);
			}

			public IEnumerable<KeyValuePair<string, string>> CreateDistributedTracePayload(ISegment segment = null)
			{
				return agentWrapperApi._distributedTracePayloadHandler.TryGetOutboundRequestHeaders(transaction, segment);
			}

			private void TryProcessDistributedTraceRequestData([NotNull] IEnumerable<KeyValuePair<string, string>> headers, [CanBeNull] string transportType)
			{
				var distributedTracePayload = agentWrapperApi._distributedTracePayloadHandler.TryDecodeInboundRequestHeaders(headers);
				if (distributedTracePayload == null)
					return;

				UpdateTransactionMetadata(distributedTracePayload, transportType);
			}

			private void UpdateTransactionMetadata([NotNull] DistributedTracePayload distributedTracePayload, [CanBeNull] string transportType)
			{
				transaction.TransactionMetadata.HasIncomingDistributedTracePayload = true;
				transaction.TransactionMetadata.DistributedTraceType = distributedTracePayload.Type;
				transaction.TransactionMetadata.DistributedTraceAccountId = distributedTracePayload.AccountId;
				transaction.TransactionMetadata.DistributedTraceAppId = distributedTracePayload.AppId;
				transaction.TransactionMetadata.DistributedTraceGuid = distributedTracePayload.Guid;
				transaction.TransactionMetadata.DistributedTraceTransportType = transportType;
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

				var errorData = ErrorData.FromException(exception, agentWrapperApi._configurationService.Configuration.StripExceptionMessages);

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

		private sealed class NoTransactionImpl : Extensions.Providers.Wrapper.ITransaction, ISegment
		{
			public bool IsValid => false;

			public ISegment ParentSegment => _noOpSegment;

			public bool IsLeaf => false;

			public string SpanId => null;

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

			public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
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

			public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata(ISegment segment = null)
			{
				Log.Debug("Tried to retrieve CAT request metadata, but there was no transaction");

				return Enumerable.Empty<KeyValuePair<string, string>>();
			}

			public void AcceptDistributedTracePayload(IEnumerable<KeyValuePair<string, string>> payload, [CanBeNull] string transportType)
			{
				Log.Debug("Tried to accept distributed trace payload, but there was no transaction");
			}

			public IEnumerable<KeyValuePair<string, string>> CreateDistributedTracePayload(ISegment segment = null)
			{
				Log.Debug("Tried to create distributed trace payload, but there was no transaction");

				return Enumerable.Empty<KeyValuePair<string, string>>();
			}

			public void NoticeError([NotNull] Exception exception)
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
