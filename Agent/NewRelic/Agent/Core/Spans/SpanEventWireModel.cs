using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using NewRelic.Collections;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Spans
{
	[JsonConverter(typeof(JsonArrayConverter))]
	[JsonObject(MemberSerialization.OptIn)]
	public class SpanEventWireModel : IHasPriority
	{
		[JsonArrayIndex(Index = 0)]
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly ReadOnlyDictionary<string, object> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly ReadOnlyDictionary<string, object> UserAttributes;


		[JsonArrayIndex(Index = 2)]
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly ReadOnlyDictionary<string, object> AgentAttributes;

		[JsonIgnore]
		public float Priority { get; }

		public SpanEventWireModel(float priority, IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes, IDictionary<string, object> agentAttributes)
		{
			Priority = priority;
			IntrinsicAttributes = new ReadOnlyDictionary<string, object>(intrinsicAttributes);
			UserAttributes = new ReadOnlyDictionary<string, object>(userAttributes);
			AgentAttributes = new ReadOnlyDictionary<string, object>(agentAttributes);
		}
	}
}
