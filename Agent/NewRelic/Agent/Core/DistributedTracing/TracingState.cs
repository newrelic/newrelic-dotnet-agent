using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using NewRelic.Core.DistributedTracing;
using System;
using System.Linq;

namespace NewRelic.Agent.Core.DistributedTracing
{
	public class TracingState
	{
		private DistributedTracePayload NewRelicPayload { get; set; }
		private W3CTraceContext TraceContext { get; set; }

		internal static TracingState ProcessInboundHeaders(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType)
		{
			var state = new TracingState();

			state.NewRelicPayload = TryGetNewRelicPayloadFromHeaders(headers, transportType);
			state.TraceContext = W3CTraceContext.TryGetTraceContextFromHeaders(headers, transportType);

			return state;
		}

		internal static IList<string> GetHeaders(string name, IEnumerable<KeyValuePair<string, string>> headers)
		{
			return headers.Where(x => x.Key.Equals(name, StringComparison.InvariantCultureIgnoreCase)).Select(v => v.Value).ToList();
		}

		private static W3CTraceContext TryGetTraceContextFromHeaders(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType)
		{
			return W3CTraceContext.TryGetTraceContextFromHeaders(headers, transportType);
		}

		private static DistributedTracePayload TryGetNewRelicPayloadFromHeaders(IEnumerable<KeyValuePair<string, string>> headers, TransportType transportType)
		{
			return null;
		}
	}
}
