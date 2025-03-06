// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CompositeTests.CrossAgentTests.HybridAgent
{
    public class SimulatedOperations
    {
        private static ExternalCallLibrary _currentExternalCall;

        public static void ExternalCall(string url, Action work)
        {
            _currentExternalCall = new();

            try
            {
                work();
            }
            finally
            {
                _currentExternalCall = null;
            }
        }

        public static ExternalCallLibrary GetCurrentExternalCall()
        {
            return _currentExternalCall;
        }

        public static string GetInjectedTraceId()
        {
            var traceParent = ParseCurrentTraceParentHeader();
            return traceParent.traceId;
        }

        public static string GetInjectedSpanId()
        {
            var traceParent = ParseCurrentTraceParentHeader();
            return traceParent.spanId;
        }

        public static bool GetInjectedSampledFlag()
        {
            var traceParent = ParseCurrentTraceParentHeader();
            return traceParent.sampled;
        }

        private static (string traceId, string spanId, bool sampled) ParseCurrentTraceParentHeader()
        {
            var traceParent = _currentExternalCall!.Headers["traceparent"];
            var fields = traceParent.Split('-');
            var isSampled = fields[3] switch
            {
                "00" => false,
                "01" => true,
                _ => throw new Exception("Unexpected trace flags value in traceparent header")
            };

            return (traceId: fields[1], spanId: fields[2], sampled: isSampled);
        }
    }

    public class ExternalCallLibrary
    {
        public IDictionary<string, string> Headers = new Dictionary<string, string>();
    }

    public class InboundContext
    {
        public string TraceId { get; set; } = string.Empty;
        public string SpanId { get; set; } = string.Empty;
        public bool Sampled { get; set; }

        public string GetTraceParentHeader()
        {
            return $"00-{TraceId}-{SpanId}-{(Sampled ? "01" : "00")}";
        }

        public string GetTraceStateHeader()
        {
            var sampledString = Sampled ? "0" : "1";
            return $"1@nr=0-0-1-123456-{SpanId}-e8b91a159289ff74-{sampledString}-{sampledString}.23456-1518469636035";
        }
    }
}
