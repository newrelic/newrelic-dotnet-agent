using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda.Events
{
	internal abstract class Event
	{
		public abstract IDictionary<String, Object> Intrinsics { get; }

		public abstract IDictionary<String, Object> UserAttributes { get; }

		public abstract IDictionary<String, Object> AgentAttributes { get; }

		/**
		 * Print an event according to the P16 data format, which is an array of 3 hashes representing intrinsics,
		 * user attributes, and agent attributes.
		 */
		public override string ToString()
		{
			return ToJsonString();
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(new IDictionary<string, object>[]{Intrinsics, UserAttributes, AgentAttributes});
		}

	}
}
