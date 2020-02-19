using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using NewRelic.Core.DistributedTracing;

namespace NewRelic.Agent.Core.DistributedTracing
{
	internal class W3CTraceContext
	{
		private W3CTraceparentHeader Traceparent { get; set; }
		private W3CTracestate Tracestate { get; set; }

		internal static W3CTraceContext TryGetTraceContextFromHeaders(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType)
		{
			var traceContext = new W3CTraceContext();
			traceContext.Traceparent = TryGetTraceparentHeaderFromHeaders(headers);
			if (traceContext.Traceparent != null)
			{
				traceContext.Tracestate = TryGetTracestateFromHeaders(headers, transportType);
				return traceContext;
			}
			else
			{
				return null;
			}
		}

		private static W3CTraceparentHeader TryGetTraceparentHeaderFromHeaders(IEnumerable<KeyValuePair<string, string>> headers)
		{
			var result = TracingState.GetHeaders("traceparent", headers);
			return result.Count == 0 ? null : new W3CTraceparentHeader(result);
		}

		private static W3CTracestate TryGetTracestateFromHeaders(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType)
		{
			var result = TracingState.GetHeaders("traceparent", headers);
			return result.Count == 0 ? null : new W3CTracestate(result);
		}
	}
}