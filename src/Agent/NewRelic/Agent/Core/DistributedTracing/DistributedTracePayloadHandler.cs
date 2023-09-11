// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
    enum DistributedTraceHeaderType
    {
        NewRelic,
        W3cTraceparent,
        W3cTracestate
    }

    public interface IDistributedTracePayloadHandler
    {
        IDistributedTracePayload TryGetOutboundDistributedTraceApiModel(IInternalTransaction transaction, ISegment segment);

        DistributedTracePayload TryDecodeInboundSerializedDistributedTracePayload(string serializedPayload);

        ITracingState AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType, DateTime transactionStartTime);

        void InsertDistributedTraceHeaders<T>(IInternalTransaction transaction, T carrier, Action<T, string, string> setter);
    }

    public class DistributedTracePayloadHandler : IDistributedTracePayloadHandler
    {
        private const string DistributedTraceTypeDefault = "App";
        private const int ParentType = 0;
        private const string TraceParentVersion = "00";
        private const string PriorityFormat = "{0:0.######}";
        private const int TraceIdMaxLength = 32;
        private readonly IConfigurationService _configurationService;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IAdaptiveSampler _adaptiveSampler;

        public DistributedTracePayloadHandler(IConfigurationService configurationService, IAgentHealthReporter agentHealthReporter, IAdaptiveSampler adaptiveSampler)
        {
            _configurationService = configurationService;
            _agentHealthReporter = agentHealthReporter;
            _adaptiveSampler = adaptiveSampler;
        }

        #region Outgoing/Create

        public void InsertDistributedTraceHeaders<T>(IInternalTransaction transaction, T carrier, Action<T, string, string> setter)
        {
            if (setter == null)
            {
                Log.Debug("setHeaders argument is null.");
                return;
            }

            try
            {
                var timestamp = DateTime.UtcNow;
                if (!_configurationService.Configuration.ExcludeNewrelicHeader)
                {
                    //set "newrelic" header
                    var distributedTracePayload = GetOutboundHeader(DistributedTraceHeaderType.NewRelic, transaction, timestamp);
                    if (!string.IsNullOrWhiteSpace(distributedTracePayload))
                    {
                        setter(carrier, Constants.DistributedTracePayloadKeyAllLower, distributedTracePayload);
                    }
                }

                var createOutboundTraceContextHeadersSuccess = false;
                try
                {
                    var traceparent = GetOutboundHeader(DistributedTraceHeaderType.W3cTraceparent, transaction, timestamp);
                    var tracestate = GetOutboundHeader(DistributedTraceHeaderType.W3cTracestate, transaction, timestamp);

                    createOutboundTraceContextHeadersSuccess = string.IsNullOrEmpty(tracestate) ? false : true;

                    if (createOutboundTraceContextHeadersSuccess)
                    {
                        setter(carrier, Constants.TraceParentHeaderKey, traceparent);
                        setter(carrier, Constants.TraceStateHeaderKey, tracestate);
                        transaction.TransactionMetadata.HasOutgoingTraceHeaders = true;
                    }
                }
                catch (Exception ex)
                {
                    _agentHealthReporter.ReportSupportabilityTraceContextCreateException();
                    Log.Error(ex, "InsertDistributedTraceHeaders() failed");
                }

                if (createOutboundTraceContextHeadersSuccess && _configurationService.Configuration.PayloadSuccessMetricsEnabled)
                {
                    _agentHealthReporter.ReportSupportabilityTraceContextCreateSuccess();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "InsertDistributedTraceHeaders() failed");
            }
        }

        private string GetOutboundHeader(DistributedTraceHeaderType headerType, IInternalTransaction transaction, DateTime timestamp)
        {
            switch (headerType)
            {
                case DistributedTraceHeaderType.W3cTraceparent:
                    return BuildTraceParent(transaction);
                case DistributedTraceHeaderType.W3cTracestate:
                    return BuildTracestate(transaction, timestamp);
                case DistributedTraceHeaderType.NewRelic:
                    var distributedTracePayload = TryGetOutboundDistributedTraceApiModelInternal(transaction, transaction.CurrentSegment, timestamp);
                    if (!distributedTracePayload.IsEmpty())
                    {
                        return distributedTracePayload.HttpSafe();
                    }
                    return string.Empty;
            }

            return string.Empty;
        }

        private string BuildTraceParent(IInternalTransaction transaction)
        {
            var traceId = transaction.TraceId;
            traceId = FormatTraceId(traceId);
            var parentId = _configurationService.Configuration.SpanEventsEnabled ? transaction.CurrentSegment.SpanId : GuidGenerator.GenerateNewRelicGuid();
            var flags = transaction.Sampled.Value ? "01" : "00";
            return $"{TraceParentVersion}-{traceId}-{parentId}-{flags}";
        }

        private string FormatTraceId(string traceId)
        {
            traceId = traceId.ToLowerInvariant();
            if (traceId.Length < 32)
            {
                traceId = traceId.PadLeft(TraceIdMaxLength, '0');
            }

            return traceId;
        }

        private string BuildTracestate(IInternalTransaction transaction, DateTime timestamp)
        {
            var accountKey = _configurationService.Configuration.TrustedAccountKey;
            var version = 0;
            var parentType = ParentType;
            var parentAccountId = _configurationService.Configuration.AccountId;
            var appId = _configurationService.Configuration.PrimaryApplicationId;
            var spanId = _configurationService.Configuration.SpanEventsEnabled ? transaction.CurrentSegment.SpanId : string.Empty;
            var transactionId = _configurationService.Configuration.TransactionEventsEnabled ? transaction.Guid : string.Empty;
            var sampled = transaction.Sampled.Value ? "1" : "0";
            var priority = string.Format(System.Globalization.CultureInfo.InvariantCulture, PriorityFormat, transaction.Priority);
            var timestampInMillis = timestamp.ToUnixTimeMilliseconds();

            var newRelicTracestate = $"{accountKey}@nr={version}-{parentType}-{parentAccountId}-{appId}-{spanId}-{transactionId}-{sampled}-{priority}-{timestampInMillis}";
            var otherVendorTracestates = string.Empty;

            if (transaction.TracingState?.VendorStateEntries != null && transaction.TracingState.VendorStateEntries.Any())
            {
                otherVendorTracestates = string.Join(",", transaction.TracingState.VendorStateEntries);
            }

            // If otherVendorTracestates is null/empty we get a trailing comma.
            if (string.IsNullOrWhiteSpace(otherVendorTracestates))
            {
                return newRelicTracestate;
            }

            return string.Join(",", newRelicTracestate, otherVendorTracestates);
        }

        public IDistributedTracePayload TryGetOutboundDistributedTraceApiModel(IInternalTransaction transaction, ISegment segment = null)
        {
            return TryGetOutboundDistributedTraceApiModelInternal(transaction, segment, DateTime.UtcNow);
        }

        private IDistributedTracePayload TryGetOutboundDistributedTraceApiModelInternal(IInternalTransaction transaction, ISegment segment, DateTime timestamp)
        {
            var accountId = _configurationService.Configuration.AccountId;
            var appId = _configurationService.Configuration.PrimaryApplicationId;

            if (!_configurationService.Configuration.SpanEventsEnabled && !_configurationService.Configuration.TransactionEventsEnabled)
            {
                Log.Finest("Did not generate payload because Span Events and Transaction Events were both disabled, preventing a traceable payload.");
                return DistributedTraceApiModel.EmptyModel;
            }

            transaction.SetSampled(_adaptiveSampler);
            var transactionIsSampled = transaction.Sampled;

            if (transactionIsSampled.HasValue == false)
            {
                Log.Error("Did not generate payload because transaction sampled value was null.");
                return DistributedTraceApiModel.EmptyModel;
            }

            var payloadGuid = _configurationService.Configuration.SpanEventsEnabled ? segment?.SpanId : null;
            var trustKey = _configurationService.Configuration.TrustedAccountKey;
            var transactionId = (_configurationService.Configuration.TransactionEventsEnabled) ? transaction.Guid : null;
            var traceId = transaction.TraceId;

            var distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(
                DistributedTraceTypeDefault,
                accountId,
                appId,
                payloadGuid,
                traceId,
                trustKey,
                transaction.Priority,
                transactionIsSampled,
                timestamp,
                transactionId);

            if (distributedTracePayload == null)
            {
                return DistributedTraceApiModel.EmptyModel;
            }

            string encodedPayload;

            try
            {
                encodedPayload = DistributedTracePayload.SerializeAndEncodeDistributedTracePayload(distributedTracePayload);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get encoded distributed trace headers for outbound request.");
                _agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadException();
                return DistributedTraceApiModel.EmptyModel;
            }

            transaction.TransactionMetadata.HasOutgoingTraceHeaders = true;

            if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceCreatePayloadSuccess();
            }

            return new DistributedTraceApiModel(encodedPayload);
        }

        #endregion Outgoing/Create

        #region Incoming/Accept

        public ITracingState AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType, DateTime transactionStartTime)
        {
            if (getter == null)
            {
                Log.Debug("getHeaders argument is null.");
                return null;
            }

            try
            {
                var tracingState = TracingState.AcceptDistributedTraceHeaders(carrier, getter, transportType, _configurationService.Configuration.TrustedAccountKey, transactionStartTime);

                if (tracingState?.IngestErrors != null)
                {
                    ReportIncomingErrors(tracingState.IngestErrors);
                }

                if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
                {
                    if (tracingState?.NewRelicPayloadWasAccepted == true)
                    {
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess();
                    }

                    if (tracingState?.TraceContextWasAccepted == true)
                    {
                        _agentHealthReporter.ReportSupportabilityTraceContextAcceptSuccess();
                    }
                }

                return tracingState;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CreateExecuteEveryTimer() failed");
                return null;
            }
        }

        private void ReportIncomingErrors(List<IngestErrorType> errors)
        {
            foreach (IngestErrorType error in errors)
            {
                switch (error)
                {
                    case IngestErrorType.Version:
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion();
                        break;

                    case IngestErrorType.NullPayload:
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull();
                        break;

                    case IngestErrorType.ParseException:
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
                        break;

                    case IngestErrorType.OtherException:
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadException();
                        break;

                    case IngestErrorType.NotTraceable:
                        Log.Debug("Incoming Guid and TransactionId were null, which is invalid for a Distributed Trace payload.");
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
                        break;

                    case IngestErrorType.NotTrusted:
                        Log.Debug($"Incoming trustKey or accountId not trusted, distributed trace payload will be ignored.");
                        _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount();
                        break;

                    case IngestErrorType.TraceContextAcceptException:
                        Log.Debug($"Generic exception occurred when accepting tracing payloads.");
                        _agentHealthReporter.ReportSupportabilityTraceContextAcceptException();
                        break;

                    case IngestErrorType.TraceContextCreateException:
                        Log.Debug($"Generic exception occurred when creating tracing payloads.");
                        _agentHealthReporter.ReportSupportabilityTraceContextCreateException();
                        break;

                    case IngestErrorType.TraceParentParseException:
                        Log.Debug($"The incoming traceparent header could not be parsed.");
                        _agentHealthReporter.ReportSupportabilityTraceContextTraceParentParseException();
                        break;

                    case IngestErrorType.TraceStateParseException:
                        Log.Debug($"The incoming tracestate header could not be parsed.");
                        _agentHealthReporter.ReportSupportabilityTraceContextTraceStateParseException();
                        break;

                    case IngestErrorType.TraceStateInvalidNrEntry:
                        Log.Debug($"The incoming tracestate header has an invalid New Relic entry.");
                        _agentHealthReporter.ReportSupportabilityTraceContextTraceStateInvalidNrEntry();
                        break;

                    case IngestErrorType.TraceStateNoNrEntry:
                        Log.Debug($"The incoming tracestate header does not contain a trusted New Relic entry.");
                        _agentHealthReporter.ReportSupportabilityTraceContextTraceStateNoNrEntry();
                        break;

                    default:
                        break;
                }
            }
        }

        public DistributedTracePayload TryDecodeInboundSerializedDistributedTracePayload(string serializedPayload)
        {
            DistributedTracePayload payload = null;

            try
            {
                payload = DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(serializedPayload);
            }
            catch (DistributedTraceAcceptPayloadVersionException)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion();
            }
            catch (DistributedTraceAcceptPayloadNullException)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredNull();
            }
            catch (DistributedTraceAcceptPayloadParseException)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();
            }
            catch (Exception)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadException();
            }

            if (!IsValidPayload(payload))
            {
                return null;
            }

            if (_configurationService.Configuration.PayloadSuccessMetricsEnabled)
            {
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadSuccess();
            }

            return payload;
        }

        private bool IsValidPayload(DistributedTracePayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            return HasTraceablePayload(payload) && HasTrustedAccountKey(payload);
        }

        private bool HasTraceablePayload(DistributedTracePayload payload)
        {
            if ((payload.Guid == null) && (payload.TransactionId == null))
            {
                Log.Debug("Incoming Guid and TransactionId were null, which is invalid for a Distributed Trace payload.");
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadParseException();

                return false;
            }

            return true;
        }

        private bool HasTrustedAccountKey(DistributedTracePayload payload)
        {
            var incomingTrustKey = payload.TrustKey ?? payload.AccountId;
            var isTrusted = incomingTrustKey == _configurationService.Configuration.TrustedAccountKey;

            if (!isTrusted)
            {
                Log.Debug($"Incoming trustKey or accountId [{incomingTrustKey}] not trusted, distributed trace payload will be ignored.");
                _agentHealthReporter.ReportSupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount();
            }

            return isTrusted;
        }

        #endregion Incoming/Accept
    }
}
