using System.Collections.Generic;
using System.Text;

namespace NewRelic.Core.DistributedTracing
{
	public class W3CTracestate
	{
		// VendorstateEntries:
		//    {
		//        { "rojo", "00f067aa0ba902b7" },
		//        { "congo", "t61rcWkgMzE" },
		//        { "abc", "ujv" },
		//        { "xyz", "mmm" },
		//    }
		// ingested order must be maintained for outgoing header

		public List<string> VendorstateEntries { get; set; }	// nonNR, nonTrusted: 55@dd=string, 45@nr=string

		// fields pulled from the trusted tracestate NR entry
		public string AccountKey { get; set; }  // "33" from "33@nr"
		public int Version { get; set; }
		public int ParentType { get; set; }
		public string AccountId { get; set; }
		public string AppId { get; set; }
		public string SpanId { get; set; }
		public string TransactionId { get; set; }
		public int Sampled { get; set; }
		public float Priority { get; set; }
		public long Timestamp { get; set; }

		public W3CTracestate(IList<string> headerValue)
		{
			// parse into fields, consolidate multiple tracestate values
			// omit guids in W3C headers if events are disabled: https://source.datanerd.us/agents/agent-specs/blob/master/distributed_tracing/Trace-Context-Payload.md#creating-a-payload-when-events-are-disabled
		}

		public W3CTracestate(List<string> vendorstates, string accountKey, int version, int parentType, string accountId, string appId, string spanId, string transactionId, int sampled, float priority, long timestamp)
		{
			VendorstateEntries = vendorstates;
			AccountKey = accountKey;
			Version = version;
			ParentType = parentType;
			AccountId = accountId;
			AppId = appId;
			SpanId = spanId;
			TransactionId = transactionId;
			Sampled = sampled;
			Priority = priority;
			Timestamp = timestamp;
		}

		public List<string> GetVendorNames()
		{
			// TODO: better string management, linq ?
			var names = new List<string>();

			// pull the vendor names from VendorstateEntries
			foreach (string entry in VendorstateEntries)
			{
				// parse out the vendor name and add to list
				names.Add(entry.Substring(0, entry.IndexOf('=')));
			}

			return names;
		}

		public KeyValuePair<string, string> ToHeaderFormat()
		{
			var sb = new StringBuilder();

			// TODO: deal with too long

			var nrEntry = $"{AccountId}@nr={Version}-{ParentType}-{AccountId}-{AppId}-{SpanId}-{TransactionId}-{Sampled.ToString()}-{Priority}-{Timestamp}";
			sb.Append(nrEntry);

			foreach (string vendorEntry in VendorstateEntries)
			{
				sb.Append($",{vendorEntry}");
			}

			return new KeyValuePair<string, string>("tracestate", sb.ToString());
		}
	}
}
