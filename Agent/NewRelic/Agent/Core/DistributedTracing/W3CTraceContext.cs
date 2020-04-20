using NewRelic.Core.DistributedTracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
	internal class W3CTraceContext
	{
		internal W3CTraceparent Traceparent { get; private set; }
		internal W3CTracestate Tracestate { get; private set; }

		internal bool TraceparentPresent { get; private set; }

		public List<string> VendorStateEntries =>Tracestate?.VendorstateEntries;

		internal static W3CTraceContext TryGetTraceContextFromHeaders(Func<string, IEnumerable<string>> getHeaders, string trustedAccountKey, IList<IngestErrorType> errors)
		{
			var traceContext = new W3CTraceContext();
			traceContext.Traceparent = traceContext.TryGetTraceParentHeaderFromHeaders(getHeaders, errors);

			if (traceContext.Traceparent != null)
			{
				traceContext.Tracestate = TryGetTracestateFromHeaders(getHeaders, trustedAccountKey, errors);
				return traceContext;
			}

			return traceContext;
		}

		private W3CTraceparent TryGetTraceParentHeaderFromHeaders(Func<string, IEnumerable<string>> getHeaders, IList<IngestErrorType> errors)
		{
			var result = getHeaders("traceparent");
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

		private static W3CTracestate TryGetTracestateFromHeaders(Func<string, IEnumerable<string>> getHeaders, string trustedAccountKey, IList<IngestErrorType> errors)
		{
			var result = getHeaders("tracestate");

			if(result == null || result.Count() == 0) 
			{
				return null;
			}

			var tracestate = W3CTracestate.GetW3CTracestateFromHeaders(result, trustedAccountKey);

			if(tracestate.Error != IngestErrorType.None) 
			{
				errors.Add(tracestate.Error);
			}

			return tracestate;
		}
	}
}