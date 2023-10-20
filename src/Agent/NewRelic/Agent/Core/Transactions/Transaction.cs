// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.Core;
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

namespace NewRelic.Agent.Core.Transactions
{
    public class Transaction : IInternalTransaction, ITransactionSegmentState
    {
        private static readonly int MaxSegmentLength = 255;

        private static readonly HashSet<string> HeadersNeedQueryParametersRemoval = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Referer", "Location", "Refresh" };

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

                int idx = currentSegmentIndex.Value;
                if (idx >= Segments.Count)
                {
                    Log.Warn($"Transaction {Guid} is out of sync with the current segment [looking for {idx} out of {Segments.Count}]");
                    return Segment.NoOpSegment;
                }
                if (Segments[idx] != null)
                {
                    return Segments[idx];
                }

                return Segment.NoOpSegment;
            }
        }

        public ITracingState TracingState { get; private set; }

        public string TraceId
        {
            get => (TracingState != null && TracingState.TraceId != null) ? TracingState.TraceId : _traceId;

            internal set => _traceId = value;
        }

        public float Priority
        {
            get => (TracingState != null && TracingState.Priority.HasValue) ? (float)TracingState.Priority : _priority;

            internal set => _priority = value;
        }

        public bool? Sampled
        {
            get => (TracingState != null && TracingState.Sampled.HasValue) ? (bool)TracingState.Sampled : _sampled;

            internal set => _sampled = value;
        }

        public void SetSampled(IAdaptiveSampler adaptiveSampler)
        {
            lock (_sync)
            {
                if (!Sampled.HasValue)
                {
                    var priority = _priority;
                    Sampled = adaptiveSampler.ComputeSampled(ref priority);
                    _priority = priority;

                }
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

                // Once the response time is captured, the only work remaining for the transaction can be
                // async work that is holding onto the transaction. We need to remove the transaction
                // from the transaction context so that it will not be reused.
                Agent._transactionService.RemoveOutstandingInternalTransactions(true, true);
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
            if (Agent.Configuration.CompleteTransactionsOnThread)
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

        public ISegment StartSegment(MethodCall methodCall)
        {
            if (Ignored)
                return Segment.NoOpSegment;

            var segment = StartSegmentImpl(methodCall);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        private Segment StartSegmentImpl(MethodCall methodCall)
        {
            var methodCallData = GetMethodCallData(methodCall);

            return new Segment(this, methodCallData);
        }

        // Used for StackExchange.Redis since we will not be instrumenting any methods when creating the many DataStore segments
        private Segment StartSegmentImpl(string typeName, string methodName, int invocationTargetHashCode, TimeSpan relativeStartTime, TimeSpan relativeEndTime)
        {
            var methodCallData = GetMethodCallData(typeName, methodName, invocationTargetHashCode);

            return new Segment(this, methodCallData, relativeStartTime, relativeEndTime);
        }

        public ISegment StartCustomSegment(MethodCall methodCall, string segmentName)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (segmentName == null)
                throw new ArgumentNullException(nameof(segmentName));


            var segment = StartSegmentImpl(methodCall);
            var customSegmentData = CreateCustomSegmentData(segmentName);

            segment.SetSegmentData(customSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public AbstractSegmentData CreateCustomSegmentData(string segmentName)
        {
            // Note: In our public docs to tells users that they must prefix their metric names with "Custom/", but there's no mechanism that actually enforces this restriction, so there's no way to know whether it'll be there or not. For consistency, we'll just strip off "Custom/" if there's at all and then we know it's consistently not there.

            if (segmentName.StartsWith("Custom/"))
                segmentName = segmentName.Substring(7);

            segmentName = Clamper.ClampLength(segmentName.Trim(), MaxSegmentLength);

            if (segmentName.Length <= 0)
                throw new ArgumentException("A segment name cannot be an empty string.");

            return new CustomSegmentData(segmentName);
        }

        public ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (brokerVendorName == null)
                throw new ArgumentNullException("brokerVendorName");


            var segment = StartSegmentImpl(methodCall);
            var messageBrokerSegmentData = CreateMessageBrokerSegmentData(destinationType, operation, brokerVendorName, destinationName);

            segment.SetSegmentData(messageBrokerSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public ISegment StartMessageBrokerSerializationSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName, string kind)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (brokerVendorName == null)
                throw new ArgumentNullException("brokerVendorName");
            if (string.IsNullOrEmpty(kind))
                throw new ArgumentNullException("kind");


            var segment = StartSegmentImpl(methodCall);
            var messageBrokerSegmentData = CreateMessageBrokerSerializationSegmentData(destinationType, operation, brokerVendorName, destinationName, kind);

            segment.SetSegmentData(messageBrokerSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public AbstractSegmentData CreateMessageBrokerSegmentData(MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName)
        {
            if (brokerVendorName == null)
                throw new ArgumentNullException("brokerVendorName");

            var action = AgentWrapperApiEnumToMetricNamesEnum(operation);
            var destType = AgentWrapperApiEnumToMetricNamesEnum(destinationType);

            return new MessageBrokerSegmentData(brokerVendorName, destinationName, destType, action);
        }

        public AbstractSegmentData CreateMessageBrokerSerializationSegmentData(MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName, string kind)
        {
            if (brokerVendorName == null)
                throw new ArgumentNullException("brokerVendorName");

            var action = AgentWrapperApiEnumToMetricNamesEnum(operation);
            var destType = AgentWrapperApiEnumToMetricNamesEnum(destinationType);

            return new MessageBrokerSerializationSegmentData(brokerVendorName, destinationName, destType, action, kind);
        }

        /// <summary>
        /// This creates a Datastore segment based on data gathered using the built-in StackExchange.Redis profiling system.
        /// </summary>
        /// <param name="invocationTargetHashCode"></param>
        /// <param name="parsedSqlStatement"></param>
        /// <param name="connectionInfo"></param>
        /// <param name="relativeStartTime"></param>
        /// <param name="relativeEndTime"></param>
        /// <returns></returns>
        public ISegment StartStackExchangeRedisSegment(int invocationTargetHashCode, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, TimeSpan relativeStartTime, TimeSpan relativeEndTime)
        {
            if (Ignored)
                return Segment.NoOpSegment;

            // Since we are not instrumenting a specific method when making these segments, this creates a stand-in method that aligns with our previous instrumentation.
            var segment = StartSegmentImpl("StackExchange.Redis.IConnectionMultiplexer", "Execute", invocationTargetHashCode, relativeStartTime, relativeEndTime);
            segment.IsLeaf = true;
            var datastoreSegmentData = CreateDatastoreSegmentData(parsedSqlStatement, connectionInfo, null, null);

            segment.SetSegmentData(datastoreSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false)
        {
            if (Ignored)
                return Segment.NoOpSegment;


            var segment = StartSegmentImpl(methodCall);
            segment.IsLeaf = isLeaf;
            var datastoreSegmentData = CreateDatastoreSegmentData(parsedSqlStatement, connectionInfo, commandText, queryParameters);

            segment.SetSegmentData(datastoreSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public IDatastoreSegmentData CreateDatastoreSegmentData(ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo, string commandText, IDictionary<string, IConvertible> queryParameters = null)
        {
            if (!Agent.Configuration.DatastoreTracerQueryParametersEnabled)
            {
                queryParameters = null;
            }

            if (parsedSqlStatement == null)
            {
                Log.Error("StartDatastoreSegment - parsedSqlStatement is null. The parsedSqlStatement should never be null. This indicates that the instrumentation was unable to parse a datastore statement.");
                parsedSqlStatement = new ParsedSqlStatement(DatastoreVendor.Other, null, null);
            }

            return new DatastoreSegmentData(_databaseService, parsedSqlStatement, commandText, connectionInfo, GetNormalizedQueryParameters(queryParameters));
        }

        private static MethodCallData GetMethodCallData(MethodCall methodCall)
        {
            var typeName = methodCall.Method.Type.FullName ?? "[unknown]";
            var methodName = methodCall.Method.MethodName;
            var invocationTargetHashCode = RuntimeHelpers.GetHashCode(methodCall.InvocationTarget);
            return new MethodCallData(typeName, methodName, invocationTargetHashCode, methodCall.IsAsync);
        }

        // Used for StackExchange.Redis since we will not be instrumenting any methods when creating the many DataStore segments
        private static MethodCallData GetMethodCallData(string typeName, string methodName, int invocationTargetHashCode)
        {
            return new MethodCallData(typeName, methodName, invocationTargetHashCode, true); // assume async
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
                    Log.Debug(e, "Error while normalizing query parameters");
                }
            }

            return normalizedQueryParameters;
        }

        private string GetTruncatedString(string originalString)
        {
            if (originalString.Length <= Agent.QueryParameterMaxStringLength)
            {
                return originalString;
            }

            return originalString.Substring(0, Agent.QueryParameterMaxStringLength);
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

        public ISegment StartExternalRequestSegmentImpl(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (destinationUri == null)
                throw new ArgumentNullException(nameof(destinationUri));
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (!destinationUri.IsAbsoluteUri)
                throw new ArgumentException("Must use an absolute URI, not a relative URI", nameof(destinationUri));

            var segment = StartSegmentImpl(methodCall);
            var externalSegmentData = CreateExternalSegmentData(destinationUri, method);

            segment.IsLeaf = isLeaf;

            segment.SetSegmentData(externalSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method)
        {
            return StartExternalRequestSegmentImpl(methodCall, destinationUri, method, false);
        }

        public ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf = false)
        {
            return StartExternalRequestSegmentImpl(methodCall, destinationUri, method, isLeaf);
        }


        public IExternalSegmentData CreateExternalSegmentData(Uri destinationUri, string method)
        {
            if (destinationUri == null)
                throw new ArgumentNullException(nameof(destinationUri));
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (!destinationUri.IsAbsoluteUri)
                throw new ArgumentException("Must use an absolute URI, not a relative URI", nameof(destinationUri));

            return new ExternalSegmentData(destinationUri, method);
        }

        public ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (typeName == null)
                throw new ArgumentNullException(nameof(typeName));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));


            var segment = StartSegmentImpl(methodCall);
            segment.IsLeaf = isLeaf;

            var methodSegmentData = CreateMethodSegmentData(typeName, methodName);


            segment.SetSegmentData(methodSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public AbstractSegmentData CreateMethodSegmentData(string typeName, string methodName)
        {
            if (typeName == null)
                throw new ArgumentNullException(nameof(typeName));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));

            return new MethodSegmentData(typeName, methodName);
        }

        public ISegment StartTransactionSegment(MethodCall methodCall, string segmentDisplayName)
        {
            if (Ignored)
                return Segment.NoOpSegment;
            if (segmentDisplayName == null)
                throw new ArgumentNullException("segmentDisplayName");


            var segment = StartSegmentImpl(methodCall);
            var simpleSegmentData = CreateSimpleSegmentData(segmentDisplayName);

            segment.SetSegmentData(simpleSegmentData);

            if (Log.IsFinestEnabled) LogFinest($"Segment start {{{segment.ToStringForFinestLogging()}}}");

            return segment;
        }

        public AbstractSegmentData CreateSimpleSegmentData(string segmentDisplayName)
        {
            if (segmentDisplayName == null)
                throw new ArgumentNullException("segmentDisplayName");

            return new SimpleSegmentData(segmentDisplayName);
        }

        public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            // start with an empty enumerable
            var headers = Enumerable.Empty<KeyValuePair<string, string>>();

            // Add the synthetics headers
            headers = headers.Concat(Agent._syntheticsHeaderHandler.TryGetOutboundSyntheticsRequestHeader(this));

            // DT/W3C
            if (_configuration.DistributedTracingEnabled)
            {
                var payload = CreateDistributedTracePayload();
                if (payload.IsEmpty())
                {
                    return headers;
                }
                return headers.Concat(new[] { new KeyValuePair<string, string>(Constants.DistributedTracePayloadKeyAllLower, payload.HttpSafe()) });
            }

            // CAT
            var currentTransactionName = CandidateTransactionName.CurrentTransactionName;
            var currentTransactionMetricName =
                Agent._transactionMetricNameMaker.GetTransactionMetricName(currentTransactionName);

            UpdatePathHash(currentTransactionMetricName);

            return headers
                .Concat(Agent._catHeaderHandler.TryGetOutboundRequestHeaders(this));
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

        private void UpdatePathHash(TransactionMetricName transactionMetricName)
        {
            var pathHash = Agent._pathHashMaker.CalculatePathHash(transactionMetricName.PrefixedName, TransactionMetadata.CrossApplicationReferrerPathHash);
            TransactionMetadata.SetCrossApplicationPathHash(pathHash);
        }

        public void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType)
        {
            if (_configuration.DistributedTracingEnabled)
            {
                // Headers have already been received, do not allow multiple calls to AcceptDistributedTraceHeaders
                if (TracingState != null)
                {
                    // using legacy supportability metric, as one for TraceContext was not spec'd
                    Agent._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMultiple();
                    return;
                }
                if (TransactionMetadata.HasOutgoingTraceHeaders)
                {
                    Agent._agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept();
                    return;
                }

                var isUnknownTransportType = transportType < TransportType.Unknown || transportType > TransportType.Other;
                var normalizedTransportType = isUnknownTransportType ? TransportType.Unknown : transportType;

                TracingState = _distributedTracePayloadHandler.AcceptDistributedTraceHeaders(carrier, getter, normalizedTransportType, StartTime);
            }
            else
            {
                // NOTE: the key for this dictionary should NOT be case sensitive since HTTP headers are not case sensitive.
                // See: https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2

                // get CAT headers X-NewRelic-Id, X-NewRelic-Transaction, X-NewRelic-App-Data
                Agent.TryProcessCatRequestData(this, carrier, getter);
            }

            Agent.TryProcessSyntheticsData(this, carrier, getter);
        }

        public IDistributedTracePayload CreateDistributedTracePayload()
        {
            if (!_configuration.DistributedTracingEnabled)
            {
                return DistributedTraceApiModel.EmptyModel;
            }

            return Agent._distributedTracePayloadHandler.TryGetOutboundDistributedTraceApiModel(this, CurrentSegment);
        }

        public void NoticeError(Exception exception)
        {
            if (Log.IsDebugEnabled) Log.Debug($"Noticed application error: {exception}");

            if (!_errorService.ShouldIgnoreException(exception))
            {
                var errorData = _errorService.FromException(exception);

                TransactionMetadata.TransactionErrorState.AddExceptionData(errorData);
                TryNoticeErrorOnCurrentSpan(errorData);
            }
            else
            {
                TransactionMetadata.TransactionErrorState.SetIgnoreAgentNoticedErrors();
            }
        }

        public void NoticeError(ErrorData errorData)
        {

            TransactionMetadata.TransactionErrorState.AddCustomErrorData(errorData);
            TryNoticeErrorOnCurrentSpan(errorData);
        }

        private void TryNoticeErrorOnCurrentSpan(ErrorData errorData)
        {
            var currentSpan = CurrentSegment as IInternalSpan;
            if (currentSpan != null) currentSpan.ErrorData = errorData;
        }

        public void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null)
        {
            TransactionMetadata.SetHttpResponseStatusCode(statusCode, subStatusCode, _errorService);
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

            var segmentApi = (Segment)segment;
            var externalSegmentData = segmentApi.Data as ExternalSegmentData;
            if (externalSegmentData == null)
            {
                throw new Exception(
                    $"Expected segment data of type {typeof(ExternalSegmentData).FullName} but received segment data of type {segmentApi.Data.GetType().FullName}");
            }

            // NOTE: the key for this dictionary should NOT be case sensitive since HTTP headers are not case sensitive.
            // See: https://www.w3.org/Protocols/rfc2616/rfc2616-sec4.html#sec4.2
            var headerDictionary = headers.ToDictionary(equalityComparer: StringComparer.OrdinalIgnoreCase);
            var responseData = Agent._catHeaderHandler.TryDecodeInboundResponseHeaders(headerDictionary);
            externalSegmentData.CrossApplicationResponseData = responseData;

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

        public void SetKafkaMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri)
        {
            var trxName = TransactionName.ForKafkaBrokerTransaction(destinationType, brokerVendorName, destination);
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

        public void SetRequestMethod(string requestMethod)
        {
            if (requestMethod == null)
            {
                throw new ArgumentNullException(nameof(requestMethod));
            }

            TransactionMetadata.SetRequestMethod(requestMethod);
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

            // Always strip query string parameters on a referrer.
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
                    var paramAttribute = _attribDefs.GetRequestParameterAttribute(parameter.Key);
                    TransactionMetadata.UserAndRequestAttributes.TrySetValue(paramAttribute, parameter.Value);
                }
            }
        }

        public ITransaction AddCustomAttribute(string key, object value)
        {
            //This code is in addition to the validation on the Attribute
            //because the values are stored in a dictionary and a null value
            //would cause an exception.
            if (key == null)
            {
                Log.Debug($"AddCustomAttribute - Unable to set custom value on transaction because the key is null/empty");
                return this;
            }

            var customAttrib = _attribDefs.GetCustomAttributeForTransaction(key);
            TransactionMetadata.UserAndRequestAttributes.TrySetValue(customAttrib, value);

            return this;
        }

        private ConcurrentList<LogEventWireModel> _logEvents = new ConcurrentList<LogEventWireModel>();

        public IList<LogEventWireModel> HarvestLogEvents()
        {
            return Interlocked.Exchange(ref _logEvents, null);
        }

        public bool AddLogEvent(LogEventWireModel logEvent)
        {
            try
            {
                _logEvents.Add(logEvent);
                return true;
            }
            catch (Exception)
            {
                // We will hit this if logs have been harvested from the transaction already...
            }
            return false;
        }

        private readonly ConcurrentList<Segment> _segments = new ConcurrentList<Segment>();
        public IList<Segment> Segments { get => _segments; }

        private readonly ISimpleTimer _timer;

        private TimeSpan? _forcedDuration;

        //leveraging boxing so that we can use Interlocked.CompareExchange instead of a lock
        private volatile object _responseTime;

        private volatile bool _ignored;
        private int _unitOfWorkCount;
        private int _totalNestedTransactionAttempts;
        private readonly int _transactionTracerMaxSegments;

        public string Guid => _guid;
        private string _guid;

        private volatile bool _ignoreAutoBrowserMonitoring;
        private volatile bool _ignoreAllBrowserMonitoring;
        private bool _ignoreApdex;

        public ICandidateTransactionName CandidateTransactionName { get; }
        public ITransactionMetadata TransactionMetadata { get; }
        public int UnitOfWorkCount => _unitOfWorkCount;
        public int NestedTransactionAttempts => _totalNestedTransactionAttempts;

        public ICallStackManager CallStackManager { get; }

        private readonly IDatabaseService _databaseService;
        private readonly IDatabaseStatementParser _databaseStatementParser;

        public IErrorService ErrorService => _errorService;
        private readonly IErrorService _errorService;
        private readonly IConfiguration _configuration;

        private readonly IDistributedTracePayloadHandler _distributedTracePayloadHandler;
        public IAttributeDefinitions AttribDefs => _attribDefs;
        private readonly IAttributeDefinitions _attribDefs;

        private object _wrapperToken;
        private readonly object _sync = new object();
        private volatile float _priority;
        private bool? _sampled;
        private volatile string _traceId;

        public Transaction(IConfiguration configuration, ITransactionName initialTransactionName,
            ISimpleTimer timer, DateTime startTime, ICallStackManager callStackManager, IDatabaseService databaseService,
            float priority, IDatabaseStatementParser databaseStatementParser, IDistributedTracePayloadHandler distributedTracePayloadHandler,
            IErrorService errorService, IAttributeDefinitions attribDefs)
        {
            CandidateTransactionName = new CandidateTransactionName(this, initialTransactionName);
            _guid = GuidGenerator.GenerateNewRelicGuid();
            _configuration = configuration;
            Priority = priority;
            _traceId = configuration.DistributedTracingEnabled ? GuidGenerator.GenerateNewRelicTraceId() : null;
            TransactionMetadata = new TransactionMetadata(_guid);

            CallStackManager = callStackManager;
            _transactionTracerMaxSegments = configuration.TransactionTracerMaxSegments;
            _startTime = startTime;
            _timer = timer;
            _unitOfWorkCount = 1;
            _databaseService = databaseService;
            _databaseStatementParser = databaseStatementParser;
            _errorService = errorService;
            _distributedTracePayloadHandler = distributedTracePayloadHandler;
            _attribDefs = attribDefs;
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

            return new ImmutableTransaction(transactionName, Segments, transactionMetadata, _startTime, _forcedDuration ?? _timer.Duration, ResponseTime, _guid, _ignoreAutoBrowserMonitoring, _ignoreAllBrowserMonitoring, _ignoreApdex, Priority, Sampled, TraceId, TracingState, AttribDefs);
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
                var transactionMetricName = Agent?._transactionMetricNameMaker?.GetTransactionMetricName(transactionName);
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

        private readonly DateTime _startTime;
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
            var id = -1;
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

                if (segment.ErrorData != null)
                {
                    TransactionMetadata.TransactionErrorState.TrySetSpanIdForErrorData(segment.ErrorData, segment.SpanId);
                }

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

        public void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter)
        {
            if (_configuration.DistributedTracingEnabled)
            {
                _distributedTracePayloadHandler.InsertDistributedTraceHeaders(this, carrier, setter);
            }
            else
            {
                var headers = GetRequestMetadata().Where(header => header.Key != null);
                foreach (var header in headers)
                {
                    setter(carrier, header.Key, header.Value);
                }
            }
        }

        public ITransaction SetRequestHeaders<T>(T headers, IEnumerable<string> keysToCapture, Func<T, string, string> getter)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            if (keysToCapture == null)
            {
                throw new ArgumentNullException(nameof(keysToCapture));
            }

            if (getter == null)
            {
                throw new ArgumentNullException(nameof(getter));
            }

            if (_configuration.HighSecurityModeEnabled)
            {
                return this;
            }

            foreach (var key in keysToCapture)
            {
                var value = getter(headers, key);

                if (HeadersNeedQueryParametersRemoval.Contains(key))
                {
                    value = RemoveQueryParameters(value);
                }

                if (value != null)
                {
                    var paramAttribute = _attribDefs.GetRequestHeadersAttribute(key.ToLowerInvariant());
                    TransactionMetadata.UserAndRequestAttributes.TrySetValue(paramAttribute, value);
                }
            }

            return this;
        }

        private string RemoveQueryParameters(string url)
        {
            if (string.IsNullOrEmpty(url) || url.Length < 2)
            {
                return url;
            }

            var index = url.IndexOf('?');
            if (index > -1)
            {
                return url.Substring(0, index);
            }

            return url;
        }

        /// <summary>
        /// Sets a User Id to be associated with this transaction.
        /// </summary>
        /// <param name="userid">The User Id for this transaction.</param>
        public void SetUserId(string userid)
        {
            if (!string.IsNullOrWhiteSpace(userid))
            {
                TransactionMetadata.UserAndRequestAttributes.TrySetValue(_attribDefs.EndUserId, userid);
            }
        }
    }
}
