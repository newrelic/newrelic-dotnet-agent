// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public class W3CTraceContext
    {
        internal W3CTraceparent Traceparent { get; private set; }
        internal W3CTracestate Tracestate { get; private set; }

        internal bool TraceparentPresent { get; private set; }

        public List<string> VendorStateEntries => Tracestate?.VendorstateEntries;

        internal static W3CTraceContext TryGetTraceContextFromHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, string trustedAccountKey, IList<IngestErrorType> errors)
        {
            var traceContext = new W3CTraceContext();
            traceContext.Traceparent = traceContext.TryGetTraceParentHeaderFromHeaders(carrier, getter, errors);

            if (traceContext.Traceparent != null)
            {
                traceContext.Tracestate = TryGetTracestateFromHeaders(carrier, getter, trustedAccountKey, errors);
                return traceContext;
            }

            return traceContext;
        }

        private W3CTraceparent TryGetTraceParentHeaderFromHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, IList<IngestErrorType> errors)
        {
            var result = getter(carrier, "traceparent");
            if (result == null || result.Count() != 1)
            {
                return null;
            }

            TraceparentPresent = true;

            var traceparent = W3CTraceparent.GetW3CTraceParentFromHeader(result.First());

            if (traceparent == null)
            {
                errors.Add(IngestErrorType.TraceParentParseException);
            }

            return traceparent;
        }

        private static W3CTracestate TryGetTracestateFromHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, string trustedAccountKey, IList<IngestErrorType> errors)
        {
            var result = getter(carrier, "tracestate");

            if (result == null || result.Count() == 0)
            {
                return null;
            }

            var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(result, trustedAccountKey);

            if (tracestate.Error != IngestErrorType.None)
            {
                errors.Add(tracestate.Error);
            }

            return tracestate;
        }
    }
}
