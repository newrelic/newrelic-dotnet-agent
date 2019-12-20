using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using NewRelic.Parsing;
using NewRelic.SystemExtensions.Collections.Generic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	public class Transaction : IInternalTransaction, ITransactionSegmentState
	{
		private static readonly int MaxSegmentLength = 255;

		private Agent _agent;
		private Agent Agent => _agent ?? (_agent = Agent.Instance);

		public bool IsValid => true;

		public ISegment CurrentSegment
		{
			get
			{
				var currentSegmentIndex = CallStackManager.TryPeek();
				if (!currentSegmentIndex.HasValue)
				{
					return Segment.NoOpSegment;
				}

				return Segments[currentSegmentIndex.Value] ?? Segment.NoOpSegment;
			}
		}

		public void End(bool captureResponseTime = true)
		{
			if (IsFinished)
			{
				LogFinest("Transaction has already been ended.");
				return;
			}

			RollupTransactionNameByStatusCodeIfNeeded();

			if (captureResponseTime)
			{
				//This only does something once so it's safe to perform each time EndTransaction is called
				var previouslyCapturedResponseTime = !TryCaptureResponseTime();
				if (previouslyCapturedResponseTime)
				{
					LogFinest("Transaction has already captured the response time.");
				}
				else
				{
					LogFinest("Response time captured.");
				}
			}

			var remainingUnitsOfWork = NoticeUnitOfWorkEnds();

			// There is still work to do, so delay ending transaction until there no more work left
			if (remainingUnitsOfWork > 0)
			{
				return;
			}

			// We want to finish then transaction as fast as possible to get the more accurate possible response time
			var finishedTransaction = Agent._transactionFinalizer.Finish(this);

			if (!finishedTransaction) return;

			// We also want to remove the transaction from the transaction context before returning so that it won't be reused
			Agent._transactionService.RemoveOutstandingInternalTransactions(true, true);

			var timer = Agent._agentTimerService.StartNew("TransformDelay");
			Action transformWork = () =>
			{
				timer?.StopAndRecordMetric();
				Agent._transactionTransformer.Transform(this);
			};

			// The completion of transactions can be run on thread or off thread. We made this configurable.  
			if (Agent._configurationService.Configuration.CompleteTransactionsOnThread)
			{
				transformWork.CatchAndLog();
			}
			else
			{
				Agent._threadPoolStatic.QueueUserWorkItem(_ => transformWork.CatchAndLog());
			}
		}

		public void Dispose()
		{
			End();
		}

		public ISegment StartCustomSegment(MethodCall methodCall, string segmentName)
		{
			if (Ignored)
				return Segment.NoOpSegment;
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
			var segment = new TypedSegment<CustomSegmentData>(this, methodCallData, new CustomSegmentData(segmentName));

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;
		}

		public ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
		{
			if (Ignored)
				return Segment.NoOpSegment;
			if (brokerVendorName == null)
				throw new ArgumentNullException("brokerVendorName");

			var action = AgentWrapperApiEnumToMetricNamesEnum(operation);
			var destType = AgentWrapperApiEnumToMetricNamesEnum(destinationType);
			var methodCallData = GetMethodCallData(methodCall);

			var segment = new TypedSegment<MessageBrokerSegmentData>(this, methodCallData, new MessageBrokerSegmentData(brokerVendorName, destinationName, destType, action));

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;
		}

		public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
		{
			if (Ignored)
				return Segment.NoOpSegment;

			if (!Agent._configurationService.Configuration.DatastoreTracerQueryParametersEnabled)
			{
				queryParameters = null;
			}

			if (parsedSqlStatement == null)
			{
				Log.Error("StartDatastoreSegment - parsedSqlStatement is null. The parsedSqlStatement should never be null. This indicates that the instrumentation was unable to parse a datastore statement.");
				parsedSqlStatement = new ParsedSqlStatement(DatastoreVendor.Other, null, null);
			}

			var data = new DatastoreSegmentData(parsedSqlStatement, commandText, connectionInfo, GetNormalizedQueryParameters(queryParameters));
			var segment = new TypedSegment<DatastoreSegmentData>(this, GetMethodCallData(methodCall), data);
			segment.IsLeaf = isLeaf;

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;
		}

		private static MethodCallData GetMethodCallData(MethodCall methodCall)
		{
			var typeName = methodCall.Method.Type.FullName ?? "[unknown]";
			var methodName = methodCall.Method.MethodName;
			var invocationTargetHashCode = RuntimeHelpers.GetHashCode(methodCall.InvocationTarget);
			return new MethodCallData(typeName, methodName, invocationTargetHashCode);
		}

		private static MetricNames.MessageBrokerDestinationType AgentWrapperApiEnumToMetricNamesEnum(
			MessageBrokerDestinationType wrapper)
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
			if (originalString.Length <= Wrapper.AgentWrapperApi.Agent.QueryParameterMaxStringLength)
			{
				return originalString;
			}

			return originalString.Substring(0, Wrapper.AgentWrapperApi.Agent.QueryParameterMaxStringLength);
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
			if (Ignored)
				return Segment.NoOpSegment;
			if (destinationUri == null)
				throw new ArgumentNullException(nameof(destinationUri));
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			if (!destinationUri.IsAbsoluteUri)
				throw new ArgumentException("Must use an absolute URI, not a relative URI", nameof(destinationUri));

			var methodCallData = GetMethodCallData(methodCall);
			var segment = new TypedSegment<ExternalSegmentData>(this, methodCallData,
				new ExternalSegmentData(destinationUri, method));
			segment.IsExternal = true;
			segment.IsLeaf = isLeaf;

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;
			// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
		}

		public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false)
		{
			if (Ignored)
				return Segment.NoOpSegment;
			if (typeName == null)
				throw new ArgumentNullException(nameof(typeName));
			if (methodName == null)
				throw new ArgumentNullException(nameof(methodName));

			var methodCallData = GetMethodCallData(methodCall);

			var segment = new TypedSegment<MethodSegmentData>(this, methodCallData, new MethodSegmentData(typeName, methodName));
			segment.IsLeaf = isLeaf;

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;

			// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
		}

		public ISegment StartTransactionSegment(MethodCall methodCall, string segmentDisplayName)
		{
			if (Ignored)
				return Segment.NoOpSegment;
			if (segmentDisplayName == null)
				throw new ArgumentNullException("segmentDisplayName");

			var methodCallData = GetMethodCallData(methodCall);
			var segment = new TypedSegment<SimpleSegmentData>(this, methodCallData, new SimpleSegmentData(segmentDisplayName));

			if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

			return segment;

			// TODO: add support for allowAutoStackTraces (or find a way to eliminate it)
		}

		public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
		{
			if (Agent._configurationService.Configuration.DistributedTracingEnabled)
			{
				var payload = CreateDistributedTracePayload();
				if (payload.IsEmpty())
				{
					return Enumerable.Empty<KeyValuePair<string, string>>();
				}
				return new[] { new KeyValuePair<string, string>(Constants.DistributedTracePayloadKey, payload.HttpSafe()) };
			}

			var headers = Enumerable.Empty<KeyValuePair<string, string>>();

			var currentTransactionName = CandidateTransactionName.CurrentTransactionName;
			var currentTransactionMetricName =
				Agent._transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

			UpdatePathHash(currentTransactionMetricName);

			return headers
				.Concat(Agent._catHeaderHandler.TryGetOutboundRequestHeaders(this))
				.Concat(Agent._syntheticsHeaderHandler.TryGetOutboundSyntheticsRequestHeader(this));
		}

		public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
		{
			var headers = Enumerable.Empty<KeyValuePair<string, string>>();

			// A CAT response header should only be sent if we had a valid CAT inbound request
			if (TransactionMetadata.CrossApplicationReferrerProcessId == null || Ignored)
			{
				return headers;
			}

			RollupTransactionNameByStatusCodeIfNeeded();

			// freeze transaction name so it doesn't change after we report it back to the caller app that made the external request
			CandidateTransactionName.Freeze(TransactionNameFreezeReason.CrossApplicationTracing);

			var currentTransactionName = CandidateTransactionName.CurrentTransactionName;
			var currentTransactionMetricName =
				Agent._transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

			UpdatePathHash(currentTransactionMetricName);
			TransactionMetadata.SetCrossApplicationResponseTimeInSeconds(
				(float)GetDurationUntilNow().TotalSeconds);

			//We should only add outbound CAT headers here (not synthetics) given that this is a Response to a request
			return headers
				.Concat(Agent._catHeaderHandler.TryGetOutboundResponseHeaders(this, currentTransactionMetricName));
		}

		public Dictionary<string, string> GetLinkingMetadata()
		{
			// todo: implementation
			Dictionary<string, string> metadata = new Dictionary<string, string>()
			{
				{ "trace.id", "valTraceId" },
				{ "span.id", "valSpanId" },
				{ "entity.name", "valEntityName" },
				{ "entity.type", "valEntityType" },
				{ "entity.guid", "valEntityGuid" },
				{ "hostname", "valHostname" },
			};

			return metadata;
		}

		private void UpdatePathHash(TransactionMetricName transactionMetricName)
		{
			var pathHash = Agent._pathHashMaker.CalculatePathHash(transactionMetricName.PrefixedName, TransactionMetadata.CrossApplicationReferrerPathHash);
			TransactionMetadata.SetCrossApplicationPathHash(pathHash);
		}

		public void AcceptDistributedTracePayload(string payload, TransportType transportType)
		{
			if (!Agent._configurationService.Configuration.DistributedTracingEnabled)
			{
				return;
			}
			if (TransactionMetadata.HasOutgoingDistributedTracePayload)
			{
				Agent._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept();
				return;
			}
			if (TransactionMetadata.HasIncomingDistributedTracePayload)
			{
				Agent._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple();
				return;
			}

			TryProcessDistributedTraceRequestData(payload, transportType);
		}

		public IDistributedTracePayload CreateDistributedTracePayload()
		{
			if (!Agent._configurationService.Configuration.DistributedTracingEnabled)
			{
				return DistributedTraceApiModel.EmptyModel;
			}

			return Agent._distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(this, CurrentSegment);
		}

		private void TryProcessDistributedTraceRequestData(string payload, TransportType transportType)
		{
			var distributedTracePayload = Agent._distributedTracePayloadHandler.TryDecodeInboundSerializedDistributedTracePayload(payload);

			if (distributedTracePayload == null)
				return;

			UpdateTransactionMetadata(distributedTracePayload, transportType);
		}

		private void UpdateTransactionMetadata(DistributedTracePayload distributedTracePayload, TransportType transportType)
		{
			TransactionMetadata.HasIncomingDistributedTracePayload = true;
			TransactionMetadata.DistributedTraceType = distributedTracePayload.Type;
			TransactionMetadata.DistributedTraceAccountId = distributedTracePayload.AccountId;
			TransactionMetadata.DistributedTraceAppId = distributedTracePayload.AppId;
			TransactionMetadata.DistributedTraceGuid = distributedTracePayload.Guid;
			TransactionMetadata.SetDistributedTraceTransportType(transportType);
			TransactionMetadata.DistributedTraceTransportDuration = ComputeDuration(StartTime, distributedTracePayload.Timestamp);
			TransactionMetadata.DistributedTraceTraceId = distributedTracePayload.TraceId;
			TransactionMetadata.DistributedTraceTrustKey = distributedTracePayload.TrustKey;
			TransactionMetadata.DistributedTraceTransactionId = distributedTracePayload.TransactionId;

			if (distributedTracePayload.Priority.HasValue)
			{
				TransactionMetadata.Priority = distributedTracePayload.Priority.Value;
			}
			TransactionMetadata.DistributedTraceSampled = distributedTracePayload.Sampled;
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
			if (!ErrorData.ShouldIgnoreError(exceptionTypeName, Agent._configurationService))
			{
				var errorData = ErrorData.FromException(exception,
					Agent._configurationService.Configuration.StripExceptionMessages);

				TransactionMetadata.AddExceptionData(errorData);
			}
		}

		public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null)
		{
			TransactionMetadata.SetHttpResponseStatusCode(statusCode, subStatusCode);
		}

		public void AttachToAsync()
		{
			var isAdded = Agent._transactionService.SetTransactionOnAsyncContext(this);
			if (!isAdded)
			{
				Log.Debug(
					"Failed to add transaction to async storage. This can occur when there are no async context providers.");
			}

			CallStackManager.AttachToAsync();
		}

		public void Detach()
		{
			Agent.Detach(true, true);
			if (Log.IsFinestEnabled) LogFinest($"Detaching from all storage contexts.");
		}

		public void DetachFromPrimary()
		{
			Agent.Detach(false, true);
			if (Log.IsFinestEnabled) LogFinest($"Detaching from primary storage contexts.");
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
			var responseData = Agent._catHeaderHandler.TryDecodeInboundResponseHeaders(headerDictionary);
			externalSegment.TypedData.CrossApplicationResponseData = responseData;

			if (responseData != null)
			{
				TransactionMetadata.MarkHasCatResponseHeaders();
			}
		}

		public void Hold()
		{
			NoticeUnitOfWorkBegins();
		}

		public void Release()
		{
			End(captureResponseTime: false);
		}

		private void SetTransactionName(ITransactionName transactionName, TransactionNamePriority priority)
		{
			CandidateTransactionName.TrySet(transactionName, priority);
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
			var normalizedPath = Agent._metricNameService.NormalizeUrl(cleanedPath);

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
			var trxName = TransactionName.ForCustomTransaction(CandidateTransactionName.CurrentTransactionName.IsWeb, name, MaxSegmentLength);
			SetTransactionName(trxName, priority);
		}

		public void RollupTransactionNameByStatusCodeIfNeeded()
		{
			if (CandidateTransactionName.CurrentTransactionName.IsWeb && TransactionMetadata.HttpResponseStatusCode >= 300)
			{
				SetTransactionName(TransactionName.ForWebTransaction(WebTransactionType.StatusCode, TransactionMetadata.HttpResponseStatusCode.ToString()), TransactionNamePriority.StatusCode);
			}
		}

		public void SetUri(string uri)
		{
			if (uri == null)
			{
				throw new ArgumentNullException(nameof(uri));
			}

			var cleanUri = StringsHelper.CleanUri(uri);

			TransactionMetadata.SetUri(cleanUri);
		}

		public void SetOriginalUri(string uri)
		{
			if (uri == null)
			{
				throw new ArgumentNullException(nameof(uri));
			}

			var cleanUri = StringsHelper.CleanUri(uri);

			TransactionMetadata.SetOriginalUri(cleanUri);
		}

		public void SetReferrerUri(string uri)
		{
			if (uri == null)
			{
				throw new ArgumentNullException(nameof(uri));
			}

			// Always strip query string parameters on a referrer. Per https://newrelic.atlassian.net/browse/DOTNET-2141
			var cleanUri = StringsHelper.CleanUri(uri);

			TransactionMetadata.SetReferrerUri(cleanUri);
		}

		public void SetQueueTime(TimeSpan queueTime)
		{
			if (queueTime == null)
			{
				throw new ArgumentNullException(nameof(queueTime));
			}

			TransactionMetadata.SetQueueTime(queueTime);
		}

		public void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters)
		{
			if (parameters == null)
			{
				throw new ArgumentNullException(nameof(parameters));
			}

			foreach (var parameter in parameters)
			{
				if (parameter.Key != null && parameter.Value != null)
				{
					TransactionMetadata.AddRequestParameter(parameter.Key, parameter.Value);
				}
			}
		}


		private readonly ConcurrentList<Segment> _segments = new ConcurrentList<Segment>();
		public IList<Segment> Segments { get => _segments; }

		private readonly ITimer _timer;
		private readonly DateTime _startTime;
		private TimeSpan? _forcedDuration;

		//leveraging boxing so that we can use Interlocked.CompareExchange instead of a lock
		private volatile object _responseTime;

		private volatile bool _ignored;
		private int _unitOfWorkCount;
		private int _totalNestedTransactionAttempts;
		private readonly int _transactionTracerMaxSegments;

		private string _guid;

		private volatile bool _ignoreAutoBrowserMonitoring;
		private volatile bool _ignoreAllBrowserMonitoring;
		private bool _ignoreApdex;

		public ICandidateTransactionName CandidateTransactionName { get; }
		public ITransactionMetadata TransactionMetadata { get; }
		public int UnitOfWorkCount => _unitOfWorkCount;
		public int NestedTransactionAttempts => _totalNestedTransactionAttempts;
	
		public ICallStackManager CallStackManager { get; }

		private readonly SqlObfuscator _sqlObfuscator;
		private readonly IDatabaseStatementParser _databaseStatementParser;

		private object _wrapperToken;

		public Transaction(IConfiguration configuration, ITransactionName initialTransactionName,
			ITimer timer, DateTime startTime, ICallStackManager callStackManager, SqlObfuscator sqlObfuscator, float priority, IDatabaseStatementParser databaseStatementParser)
		{
			CandidateTransactionName = new CandidateTransactionName(this, initialTransactionName);
			_guid = GuidGenerator.GenerateNewRelicGuid();
			TransactionMetadata = new TransactionMetadata
			{
				Priority = priority,
				DistributedTraceTraceId = _guid
			};

			CallStackManager = callStackManager;
			_transactionTracerMaxSegments = configuration.TransactionTracerMaxSegments;
			_startTime = startTime;
			_timer = timer;
			_unitOfWorkCount = 1;
			_sqlObfuscator = sqlObfuscator;
			_databaseStatementParser = databaseStatementParser;
		}

		public int Add(Segment segment)
		{
			if (segment != null)
			{
				return _segments.AddAndReturnIndex(segment);
			}
			return -1;
		}

		public ImmutableTransaction ConvertToImmutableTransaction()
		{
			var transactionName = CandidateTransactionName.CurrentTransactionName;
			var transactionMetadata = TransactionMetadata.ConvertToImmutableMetadata();

			return new ImmutableTransaction(transactionName, Segments, transactionMetadata, _startTime, _forcedDuration ?? _timer.Duration, ResponseTime, _guid, _ignoreAutoBrowserMonitoring, _ignoreAllBrowserMonitoring, _ignoreApdex, _sqlObfuscator);
		}

		public void LogFinest(string message)
		{
			if (Log.IsFinestEnabled)
			{
				Log.Finest($"Trx {Guid}: {message}");
			}
		}

		public void Ignore()
		{
			_ignored = true;

			if (Log.IsFinestEnabled)
			{
				var transactionName = CandidateTransactionName.CurrentTransactionName;
				var transactionMetricName = Agent._transactionMetricNameMaker.GetTransactionMetricName(transactionName);
				var stackTrace = new StackTrace();
				Log.Finest($"Transaction \"{transactionMetricName}\" is being ignored from {stackTrace}");
			}
		}

		public int NoticeUnitOfWorkBegins()
		{
			return Interlocked.Increment(ref _unitOfWorkCount);
		}

		public int NoticeUnitOfWorkEnds()
		{
			return Interlocked.Decrement(ref _unitOfWorkCount);
		}

		public int NoticeNestedTransactionAttempt()
		{
			return Interlocked.Increment(ref _totalNestedTransactionAttempts);
		}

		public void IgnoreAutoBrowserMonitoringForThisTx()
		{
			_ignoreAutoBrowserMonitoring = true;
		}

		public void IgnoreAllBrowserMonitoringForThisTx()
		{
			_ignoreAllBrowserMonitoring = true;
		}

		public void IgnoreApdex()
		{
			_ignoreApdex = true;
		}

		#region Methods to access data
		public bool IgnoreAutoBrowserMonitoring => _ignoreAutoBrowserMonitoring;
		public bool IgnoreAllBrowserMonitoring => _ignoreAllBrowserMonitoring;

		public bool Ignored => _ignored;
		public string Guid => _guid;
		public DateTime StartTime => _startTime;

		/// <summary>
		/// This is a method instead of property to prevent StackOverflowException when our 
		/// Transaction is serialized. Sometimes 3rd party tools serialize our stuff even when 
		/// we don't want. We still want to do no harm, when possible.
		/// 
		/// Ideally, we don't return the instance in this way but putting in a quick fix for now.
		/// </summary>
		/// <returns></returns>
		public ITransactionSegmentState GetTransactionSegmentState()
		{
			return this;
		}

		private ConcurrentDictionary<string, object> _transactionCache;

		private ConcurrentDictionary<string, object> TransactionCache => _transactionCache ?? (_transactionCache = new ConcurrentDictionary<string, object>());

		public int CurrentManagedThreadId => Thread.CurrentThread.ManagedThreadId;

		public object GetOrSetValueFromCache(string key, Func<object> func)
		{
			if (key == null)
			{
				Log.Debug("GetOrSetValueFromCache(), key is NULL");
				return null;
			}

			return TransactionCache.GetOrAdd(key, x => func());
		}

		// This will need to get cleaned up with all of the timing stuff.
		// Having a timer in the transaction and then separate timers in the segments is bad.
		public TimeSpan GetDurationUntilNow()
		{
			return _timer.Duration;
		}

		public TimeSpan GetRelativeTime()
		{
			return GetDurationUntilNow();
		}

		public bool TryCaptureResponseTime()
		{
			return null == Interlocked.CompareExchange(ref _responseTime, GetDurationUntilNow(), null);
		}

		public TimeSpan? ResponseTime => _responseTime as TimeSpan?;

		#endregion

		#region TransactionBuilder finalization logic

		public bool IsFinished { get; private set; } = false;

		private object _finishLock = new object();

		public bool Finish()
		{
			_timer.Stop();
			// Prevent the finalizer/destructor from running
			GC.SuppressFinalize(this);

			if (IsFinished) return false;

			lock (_finishLock)
			{
				if (IsFinished) return false;

				//Only the call that successfully sets IsFinished to true should return true so that the transaction can only be finished once.
				IsFinished = true;
				return true;
			}
		}

		/// <summary>
		/// A destructor/finalizer that will announce when a Transaction is garbage collected without ending normally (e.g. without Finish() being called).
		/// </summary>
		~Transaction()
		{
			try
			{
				GC.SuppressFinalize(this);
				EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(this));
			}
			catch
			{
				// Swallow because throwing from a finally is fatal
			}
		}

		public void ForceChangeDuration(TimeSpan duration)
		{
			_forcedDuration = duration;
		}

		#endregion TransactionBuilder finalization logic

		public int CallStackPush(Segment segment)
		{
			int id = -1;
			if (!_ignored)
			{
				id = Add(segment);
				if (id >= 0)
				{
					CallStackManager.Push(id);
				}
			}
			return id;
		}

		public void CallStackPop(Segment segment, bool notifyParent = false)
		{
			CallStackManager.TryPop(segment.UniqueId, segment.ParentUniqueId);
			if (notifyParent)
			{
				if (Log.IsFinestEnabled) LogFinest($"Segment end {{{segment.ToStringForFinestLogging()}}}");

				if (segment.UniqueId >= _transactionTracerMaxSegments)
				{
					// we're over the segment limit.  Null out the reference to the segment.
					_segments[segment.UniqueId] = null;
				}

				if (segment.ParentUniqueId.HasValue)
				{
					var parentSegment = _segments[segment.ParentUniqueId.Value];
					if (null != parentSegment)
					{
						parentSegment.ChildFinished(segment);
					}
				}
			}
		}

		public int? ParentSegmentId()
		{
			return CallStackManager.TryPeek();
		}

		public ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql)
		{
			return _databaseStatementParser.ParseDatabaseStatement(datastoreVendor, commandType, sql);
		}

		public object GetWrapperToken()
		{
			return _wrapperToken;
		}

		public void SetWrapperToken(object wrapperToken)
		{
			_wrapperToken = wrapperToken;
		}
	}
}
