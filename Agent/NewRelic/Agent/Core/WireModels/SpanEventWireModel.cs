using System;
using System.Collections.Generic;
using MoreLinq;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	[JsonObject(MemberSerialization.OptIn)]
	public class SpanEventWireModel : IHasPriority
	{
		[JsonArrayIndex(Index = 0)] public readonly ReadOnlyDictionary<string, object> IntrinsicAttributes;
		[JsonArrayIndex(Index = 1)] public readonly ReadOnlyDictionary<string, object> UserAttributes;
		[JsonArrayIndex(Index = 2)] public readonly ReadOnlyDictionary<string, object> AgentAttributes;

		[JsonIgnore] public float Priority { get; }

		private const string PriorityKey = "priority";

		private static readonly ReadOnlyDictionary<string, object> EmptyDictionary = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

		public SpanEventWireModel(IEnumerable<KeyValuePair<string, object>> intrinsicAttributes)
		{
			IntrinsicAttributes = new ReadOnlyDictionary<string, object>(intrinsicAttributes.ToDictionary());
			UserAttributes = EmptyDictionary;
			AgentAttributes = EmptyDictionary;

			//extract priority, check type, cache it
			if (!IntrinsicAttributes.TryGetValue(PriorityKey, out var priorityAsObject) || !(priorityAsObject is float priority))
			{
				throw new ArgumentException("Span Event does not contain 'priority' that is of type 'float'", nameof(intrinsicAttributes));
			}
			Priority = priority;
		}
	}
}