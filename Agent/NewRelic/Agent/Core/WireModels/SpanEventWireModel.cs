using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using Newtonsoft.Json;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.WireModels
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

		[JsonIgnore] public float Priority { get; }
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		private static readonly ReadOnlyDictionary<string, object> EmptyDictionary = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

		public SpanEventWireModel(IDictionary<string,object> intrinsicAttributes)
		{
			const string priorityKey = "priority";

			IntrinsicAttributes = new ReadOnlyDictionary<string, object>(intrinsicAttributes);
			UserAttributes = EmptyDictionary;
			AgentAttributes = EmptyDictionary;

			//extract priority, check type, cache it
			if (!IntrinsicAttributes.TryGetValue(priorityKey, out var priorityAsObject) || !(priorityAsObject is float priority))
			{
				throw new ArgumentException("Span event does not contain 'priority' that is of type 'float'", nameof(intrinsicAttributes));
			}
			Priority = priority;
		}
	}
}