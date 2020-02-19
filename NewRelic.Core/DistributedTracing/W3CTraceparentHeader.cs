
using System.Collections.Generic;

namespace NewRelic.Core.DistributedTracing
{
	public class W3CTraceparentHeader
	{
		public const int SupportedVersion = 0;

		public int Version { get; set; }
		public string TraceId { get; set; }	// 16 bytes
		public string ParentId { get; set; } // 8 bytes
		public string TraceFlags { get; set; } // 2 bytes/8 bits

		public W3CTraceparentHeader(IList<string> headerValue)
		{
			// parse into fields
		}	
		
		public W3CTraceparentHeader(int version, string traceId, string parentId, string traceFlags)
		{
			Version = version;
			TraceId = traceId;
			ParentId = parentId;
			TraceFlags = traceFlags;
		}

		public KeyValuePair<string, string> ToHeaderFormat()
		{
			var value = $"{Version}-{TraceId}-{ParentId}-{TraceFlags}";
			return new KeyValuePair<string, string>("traceparent", value);
		}
	}
}
