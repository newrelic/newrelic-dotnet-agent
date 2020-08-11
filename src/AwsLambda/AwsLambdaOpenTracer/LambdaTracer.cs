// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using NewRelic.OpenTracing.AmazonLambda.Util;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Util;
using System;

namespace NewRelic.OpenTracing.AmazonLambda
{
    public class LambdaTracer : ITracer
    {
        private const string NEWRELIC_TRACE_HEADER = "newrelic";
        private readonly ILogger _logger;
        private const int Target = 10;
        private const int Interval = 60;
        public static readonly ITracer Instance = new LambdaTracer();

        private LambdaTracer()
        {
            _logger = new Logger();
            ScopeManager = new AsyncLocalScopeManager();
            AdaptiveSampler = new AdaptiveSampler(Target, Interval, new Random());
            AccountId = Environment.GetEnvironmentVariable("NEW_RELIC_ACCOUNT_ID") ?? string.Empty;
            TrustedAccountKey = Environment.GetEnvironmentVariable("NEW_RELIC_TRUSTED_ACCOUNT_KEY") ?? string.Empty;
            var primaryAppId = Environment.GetEnvironmentVariable("NEW_RELIC_PRIMARY_APPLICATION_ID");
            PrimaryApplicationId = string.IsNullOrEmpty(primaryAppId) ? "Unknown" : primaryAppId;

            var debug = Environment.GetEnvironmentVariable("NEW_RELIC_DEBUG_MODE");
            DebugMode = !string.IsNullOrEmpty(debug) && debug.Trim().ToLower() == "true" ? true : false;
        }

        public IScopeManager ScopeManager { get; }

        public bool DebugMode { get; }

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new LambdaSpanBuilder(operationName);
        }

        public ISpan ActiveSpan
        {
            get
            {
                IScope scope = ScopeManager.Active;
                return scope?.Span;
            }
        }

        internal AdaptiveSampler AdaptiveSampler { get; set; }

        internal static TracePriorityManager TracePriorityManager { get; } = new TracePriorityManager();

        internal string AccountId { get; set; }

        internal string TrustedAccountKey { get; set; }

        internal string PrimaryApplicationId { get; set; }

        public void Inject<TCarrier>(ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            var context = (LambdaSpanContext)spanContext;
            var span = context.GetSpan();
            var payload = DistributedTracePayload.TryBuildOutgoingPayload(
                type: "App",
                accountId: AccountId,
                appId: PrimaryApplicationId,
                guid: span.Guid(),
                traceId: span.RootSpan.DistributedTracingState.InboundPayload?.TraceId ?? span.RootSpan.TransactionState.TransactionId,
                trustKey: TrustedAccountKey,
                priority: span.RootSpan.PrioritySamplingState.Priority,
                sampled: span.RootSpan.PrioritySamplingState.Sampled,
                timestamp: span.TimeStamp.DateTime,
                transactionId: span.RootSpan.TransactionState.TransactionId
            );

            // If we couldnt build a payload just return
            if (payload == null)
                return;

            if (format.Equals(BuiltinFormats.TextMap))
            {
                // "text" version of payload
                ((ITextMap)carrier).Set(NEWRELIC_TRACE_HEADER, payload.ToJson());
            }
            else if (format.Equals(BuiltinFormats.HttpHeaders))
            {
                // "httpSafe" version of payload
                ((ITextMap)carrier).Set(NEWRELIC_TRACE_HEADER, payload.SerializeAndEncodeDistributedTracePayload());
            }
            //else if (format.Equals(BuiltinFormats.Binary))
            //{
            //	var payloadBytes = Encoding.UTF8.GetBytes(payload.ToJson());
            //	((MemoryStream)carrier).Write(payloadBytes, 0, payloadBytes.Length);
            //}
        }

        public ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            var payload = GetPayloadString(format, carrier);
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            var distributedTracePayload = DistributedTracePayload.TryDecodeAndDeserializeDistributedTracePayload(payload);
            if (distributedTracePayload == null)
            {
                var message = $"{NEWRELIC_TRACE_HEADER} header value was not accepted.";
                _logger.Log(message, false, "ERROR");
                throw new ArgumentNullException(message);
            }

            var transportDurationInMillis = (DateTimeOffset.UtcNow - distributedTracePayload.Timestamp).TotalMilliseconds;
            return new LambdaPayloadContext(distributedTracePayload, transportDurationInMillis);
        }

        private string GetPayloadString<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            if (format == null || carrier == null)
            {
                var message = $"Null format or carrier.";
                _logger.Log(message, false, "ERROR");
                throw new ArgumentNullException(message);
            }

            var payload = string.Empty;
            if (format.Equals(BuiltinFormats.TextMap))
            {
                foreach (var entry in (ITextMap)carrier)
                {
                    if (entry.Key.ToLower() == NEWRELIC_TRACE_HEADER)
                    {
                        payload = entry.Value;
                        break;
                    }
                }
            }
            else if (format.Equals(BuiltinFormats.HttpHeaders))
            {
                if (((ITextMap)carrier).GetEnumerator() == null)
                {
                    throw new ArgumentNullException("Invalid carrier.");
                }

                foreach (var entry in (ITextMap)carrier)
                {
                    if (entry.Key.ToLower() == NEWRELIC_TRACE_HEADER)
                    {
                        payload = entry.Value;
                        break;
                    }
                }
            }
            else if (format.Equals(NewRelicFormats.Payload))
            {
                payload = ((IPayload)carrier).GetPayload ?? throw new NullReferenceException();  // we expect that an error will be thrown.
            }
            // Implemented in OT master and in Java side, but not in 0.12.
            //else if(format.Equals(BuiltinFormats.Binary))
            //{
            //	var byteStream = carrier as MemoryStream;
            //	if(byteStream == null)
            //	{
            //		throw new ArgumentNullException("Invalid carrier.");
            //	}

            //	var payloadBytes = byteStream.ToArray();
            //	payload = System.Text.Encoding.UTF8.GetString(payloadBytes);
            //}
            else
            {
                var message = $"Unable to extract payload from carrier: {carrier}";
                _logger.Log(message, false, "ERROR");
                throw new ArgumentNullException(message);
            }

            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            return payload;
        }
    }
}
