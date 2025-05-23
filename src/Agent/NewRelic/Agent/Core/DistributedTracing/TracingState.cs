// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public class TracingState : ITracingState
    {
        // not valid for this enum, but will be replaced on first call to Type
        private DistributedTracingParentType _type = (DistributedTracingParentType)(-2);
        private DateTime _timestamp;
        private bool _validTracestateWasAccepted = false;
        private DateTime _transactionStartTime;

        #region Properties

        public DistributedTracingParentType Type
        {
            get
            {
                if ((int)_type != -2)
                {
                    return _type;
                }

                if (_traceContext?.Tracestate != null)
                {
                    return _type = _traceContext.Tracestate.ParentType;
                }

                if (_newRelicPayload?.Type != null)
                {
                    if (Enum.TryParse<DistributedTracingParentType>(_newRelicPayload.Type, out var payloadParentType))
                    {
                        return _type = payloadParentType;
                    }
                }

                return _type = DistributedTracingParentType.Unknown;
            }
        }

        public string AppId
        {
            get
            {
                if (_traceContext?.Tracestate != null)
                {
                    return _traceContext.Tracestate.AppId;
                }

                return _newRelicPayload?.AppId;
            }
        }

        public string AccountId
        {
            get
            {
                if (_traceContext?.Tracestate != null)
                {
                    return _traceContext.Tracestate.AccountId;
                }

                return _newRelicPayload?.AccountId;
            }
        }

        // Not part of trace-context or newrelic-payload - doesn't need to check if context or payload.
        public TransportType TransportType { get; private set; }

        public string Guid
        {
            get
            {
                if (_traceContext?.Tracestate != null)
                {
                    return _traceContext.Tracestate.SpanId;
                }

                return _newRelicPayload?.Guid;
            }
        }

        public string ParentId => _traceContext?.Traceparent?.ParentId;

        public DateTime Timestamp
        {
            get
            {
                if (_timestamp != default)
                {
                    return _timestamp;
                }

                if (_traceContext?.Tracestate != null && _traceContext.Tracestate.Timestamp != default)
                {
                    return _timestamp = _traceContext.Tracestate.Timestamp.FromUnixTimeMilliseconds();
                }

                if (_newRelicPayload?.Timestamp != default)
                {
                    return _timestamp = _newRelicPayload.Timestamp;
                }

                return _timestamp = default(DateTime); // default is same as new.
            }
        }

        public TimeSpan TransportDuration
        {
            get
            {
                var duration = Timestamp != default ? _transactionStartTime - Timestamp : TimeSpan.Zero;
                return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
            }
        }

        public string TraceId
        {
            get
            {
                if (_traceContext?.Traceparent != null)
                {
                    return _traceContext.Traceparent.TraceId;
                }

                return _newRelicPayload?.TraceId;
            }
        }

        public string TransactionId
        {
            get
            {
                if (_traceContext?.Tracestate != null)
                {
                    return _traceContext.Tracestate.TransactionId;
                }

                return _newRelicPayload?.TransactionId;
            }
        }

        private bool? _sampled;
        public bool? Sampled
        {
            get
            {
                if (_sampled is not null)
                    return _sampled;

                if (_traceContext?.Tracestate is { Sampled: not null })
                    return _sampled = Convert.ToBoolean(_traceContext.Tracestate.Sampled);

                if (_newRelicPayload?.Sampled is not null)
                    return _sampled = _newRelicPayload.Sampled;

                return null;
            }
        }

        private float? _priority;
        public float? Priority
        {
            get
            {
                if (_priority != null)
                {
                    return _priority;
                }

                if (_traceContext?.Tracestate is { Priority: not null })
                {
                    return _traceContext.Tracestate.Priority;
                }

                return _newRelicPayload?.Priority;
            }
        }

        public bool NewRelicPayloadWasAccepted { get; private set; } = false;
        public bool TraceContextWasAccepted { get; private set; } = false;

        public bool HasDataForParentAttributes => NewRelicPayloadWasAccepted || _validTracestateWasAccepted;
        public bool HasDataForAttributes { get; private set; } = false;

        public List<string> VendorStateEntries => (_traceContext?.VendorStateEntries);

        #endregion Properties

        public List<IngestErrorType> IngestErrors { get; private set; }

        private DistributedTracePayload _newRelicPayload;
        private W3CTraceContext _traceContext;

        public static ITracingState AcceptDistributedTraceHeaders<T>(
            T carrier,
            Func<T, string, IEnumerable<string>> getter,
            TransportType transportType,
            string agentTrustKey,
            DateTime transactionStartTime,
            RemoteParentSampledBehavior remoteParentSampledBehavior,
            RemoteParentSampledBehavior remoteParentNotSampledBehavior)
        {
            var tracingState = new TracingState();
            var errors = new List<IngestErrorType>();

            // w3c
            tracingState._traceContext = W3CTraceContext.TryGetTraceContextFromHeaders(carrier, getter, agentTrustKey, errors);
            tracingState.TraceContextWasAccepted =
                tracingState._traceContext.TraceparentPresent &&
                !errors.Contains(IngestErrorType.TraceParentParseException) ? true : false;

            tracingState._validTracestateWasAccepted = tracingState._traceContext?.Tracestate?.AccountKey != null ? true : false;

            tracingState.ApplyRemoteParentSampledBehavior(remoteParentSampledBehavior, remoteParentNotSampledBehavior);

            // newrelic 
            // if traceparent was present (regardless if valid), ignore newrelic header
            if (!tracingState._traceContext.TraceparentPresent)
            {
                // Search for the following header keys in this order: "newrelic", "NEWRELIC", "Newrelic"
                // If the getter function makes a case-insensitive search it will find any of the three
                // variants on the first call.
                var newRelicHeaderList = getter(carrier, Constants.DistributedTracePayloadKeyAllLower);
                if (newRelicHeaderList?.Any() == false)
                {
                    newRelicHeaderList = getter(carrier, Constants.DistributedTracePayloadKeyAllUpper);
                }
                if (newRelicHeaderList?.Any() == false)
                {
                    newRelicHeaderList = getter(carrier, Constants.DistributedTracePayloadKeySingleUpper);
                }
                if (newRelicHeaderList?.Any() == true) // a NR header key was present
                {
                    tracingState._newRelicPayload = DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(newRelicHeaderList.FirstOrDefault(), agentTrustKey, errors);
                    tracingState.NewRelicPayloadWasAccepted = tracingState._newRelicPayload != null ? true : false;
                }
            }

            if (errors.Any())
            {
                tracingState.IngestErrors = errors;
            }

            // if Traceparent was present (regardless if valid), generate TransactionAttributes
            tracingState.HasDataForAttributes = tracingState._traceContext.TraceparentPresent == true || tracingState.NewRelicPayloadWasAccepted == true;

            tracingState._transactionStartTime = tracingState._validTracestateWasAccepted || tracingState.NewRelicPayloadWasAccepted ? transactionStartTime : default;
            tracingState.TransportType = transportType;

            return tracingState;
        }

        /// <summary>
        /// Use remote parent sampled behavior configuration in conjunction with the traceparent sampled flag to determine
        /// if the transaction should be sampled.
        /// </summary>
        private void ApplyRemoteParentSampledBehavior(
            RemoteParentSampledBehavior remoteParentSampledBehavior,
            RemoteParentSampledBehavior remoteParentNotSampledBehavior)
        {
            // don't do anything if the traceparent is not present or if behavior is configured to default
            if (!_traceContext.TraceparentPresent ||
                (remoteParentNotSampledBehavior == RemoteParentSampledBehavior.Default &&
                 remoteParentSampledBehavior == RemoteParentSampledBehavior.Default))
            {
                return;
            }

            var sampledBehavior = _traceContext.Traceparent.Sampled
                ? remoteParentSampledBehavior
                : remoteParentNotSampledBehavior;

            switch (sampledBehavior)
            {
                case RemoteParentSampledBehavior.AlwaysOn:
                    _priority = 2.0f; // per the spec, set priority high so that this sample is always kept
                    _sampled = true;
                    break;
                case RemoteParentSampledBehavior.AlwaysOff:
                    _priority = 0.0f; // set lowest possible priority
                    _sampled = false;
                    break;
                // don't need a case for Default, as it is handled above
                default:
                    throw new ArgumentException($"Invalid {(_traceContext.Traceparent.Sampled ? "remoteParentSampledBehavior" : "remoteParentNotSampledBehavior")} value: {sampledBehavior}.");
            }

            Log.Finest("ApplyRemoteParentSampledBehavior:  _traceContext.Traceparent.Sampled={SampledValue}, {ParentType}SampledBehavior: {SampledBehavior} ==> Sampled: {Sampled}, Priority: {Priority}",
                    _traceContext.Traceparent.Sampled,
                    _sampled.Value ? "remoteParent" : "remoteParentNot",
                    sampledBehavior,
                    Sampled,
                    Priority
                );
        }
    }
}
