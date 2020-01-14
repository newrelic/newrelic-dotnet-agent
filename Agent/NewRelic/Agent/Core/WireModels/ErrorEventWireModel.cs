using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Collections;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(JsonArrayConverter))]
	[JsonObject(MemberSerialization.OptIn)]
	public class ErrorEventWireModel : IHasPriority
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

		private readonly bool _isSynthetics;

		private float _priority;

		public float Priority
		{
			get { return _priority; }
			set
			{
				const float priorityMin = 0.0f;
				if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
				{
					throw new ArgumentException($"Error event requires a valid priority value greater than {priorityMin}, value used: {value}");
				}
				_priority = value;
			}
		}

		public ErrorEventWireModel(IDictionary<string,object> agentAttributes, IDictionary<string,object> intrinsicAttributes, IDictionary<string,object> userAttributes, bool isSynthetics, float priority)
		{
			Priority = priority;
			IntrinsicAttributes = new ReadOnlyDictionary<string, object>(intrinsicAttributes);
			UserAttributes = new ReadOnlyDictionary<string, object>(userAttributes);
			AgentAttributes = new ReadOnlyDictionary<string, object>(agentAttributes);
			_isSynthetics = isSynthetics;
		}

		public bool IsSynthetics()
		{
			// An event will always contain either all of the synthetics keys or none of them.
			// There is no need to check for the presence of each synthetics key.
			return _isSynthetics;
		}
	}
}
