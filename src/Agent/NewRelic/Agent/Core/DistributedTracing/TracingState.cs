// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public bool? Sampled { get; private set; }

        public float? Priority
        {
            get
            {
                if (_traceContext?.Tracestate != null)
                {
                    return _traceContext.Tracestate.Priority;
                }

                if (_newRelicPayload?.Priority != null && _newRelicPayload.Priority.HasValue)
                {
                    return _newRelicPayload.Priority;
                }

                return null;
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

        private TracingState()
        {
        }

        public static ITracingState AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType, string agentTrustKey, DateTime transactionStartTime, string remoteParentSampledBehavior, string remoteParentNotSampledBehavior)
        {
            var tracingState = new TracingState();
            var errors = new List<IngestErrorType>();

            // w3c
            tracingState._traceContext = W3CTraceContext.TryGetTraceContextFromHeaders(carrier, getter, agentTrustKey, errors);
            tracingState.TraceContextWasAccepted =
                tracingState._traceContext.TraceparentPresent &&
                !errors.Contains(IngestErrorType.TraceParentParseException) ? true : false;

            tracingState._validTracestateWasAccepted = tracingState._traceContext?.Tracestate?.AccountKey != null ? true : false;

            tracingState.SetSampledAndPriority(remoteParentSampledBehavior, remoteParentNotSampledBehavior);

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

        private void SetSampledAndPriority(string remoteParentSampledBehavior, string remoteParentNotSampledBehavior)
        {
            if (_traceContext?.Tracestate is { Sampled: not null })
            {
                var sampledBool = Convert.ToBoolean(_traceContext.Tracestate.Sampled);
                var sampledBehavior = sampledBool ? remoteParentSampledBehavior : remoteParentNotSampledBehavior;

                // if there is a valid traceparent, sampling behavior is determined by configuration
                if (_traceContext.TraceparentPresent && sampledBehavior != "default")
                {
                    if (sampledBehavior == "alwaysOn")
                    {
                        _traceContext.Tracestate.Priority = 2.0f; // per the spec, set priority high so that this sample is always kept
                        Sampled = true;
                    }
                    else if (sampledBehavior == "alwaysOff")
                    {
                        _traceContext.Tracestate.Priority = 0.0f; // set lowest possible priority
                        Sampled = false;
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid {(sampledBool ? "remoteParentSampledBehavior" : "remoteParentNotSampledBehavior")}: {sampledBehavior}.");
                    }
                }
                else
                {
                    // either there's no traceparent or the behavior is default, so we use the sampled value
                    Sampled = sampledBool;
                }

                // TODO: testing only
                Log.Info($"SetSampledAndPriority:  _traceContext.Tracestate.Sampled={_traceContext.Tracestate.Sampled}, {(sampledBool ? "remoteParent" : "remoteParentNot")}SampledBehavior: {sampledBehavior} ==> Sampled: {Sampled}, Priority:{Priority}");
            }
            else
            {
                Sampled = _newRelicPayload?.Sampled;
            }
        }
    }
}
