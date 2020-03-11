using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.DistributedTracing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
	internal class W3CTraceContext
	{
		private W3CTraceparent _traceparent { get; set; }
		private W3CTracestate _tracestate { get; set; }

		public List<string> VendorStateEntries =>_tracestate?.VendorstateEntries;

		internal static W3CTraceContext TryGetTraceContextFromHeaders(Func<string, IList<string>> getHeaders, TransportType transportType, string trustedAccountKey)
		{
			var traceContext = new W3CTraceContext();
			traceContext._traceparent = TryGetTraceparentHeaderFromHeaders(getHeaders);
			if (traceContext._traceparent != null)
			{
				traceContext._tracestate = TryGetTracestateFromHeaders(getHeaders, transportType, trustedAccountKey);
				return traceContext;
			}

			return null;
		}

		private static W3CTraceparent TryGetTraceparentHeaderFromHeaders(Func<string, IList<string>> getHeaders)
		{
			var result = getHeaders("traceparent");
			if (result == null || result.Count != 1)
			{
				return null;
			}

			return W3CTraceparent.GetW3CTraceparentFromHeader(result[0]);
		}

		private static W3CTracestate TryGetTracestateFromHeaders(Func<string, IList<string>> getHeaders, TransportType transportType, string trustedAccountKey)
		{
			var result = getHeaders("tracestate");
			return result.Count == 0 ? null : W3CTracestate.GetW3CTracestateFromHeaders(result, trustedAccountKey);
		}
	}
}