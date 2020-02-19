using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Collections;
using NewRelic.Core;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{

	[JsonConverter(typeof(JsonArrayConverter))]
	public class CustomEventWireModel : IHasPriority
	{
		[JsonArrayIndex(Index = 0)]
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly IEnumerable<KeyValuePair<string, object>> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		[JsonConverter(typeof(EventAttributesJsonConverter))]
		public readonly IEnumerable<KeyValuePair<string, object>> UserAttributes;

		private float _priority;

		[JsonIgnore] 
		public float Priority
		{
			get { return _priority; }
			set
			{
				const float priorityMin = 0.0f;
				if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
				{
					throw new ArgumentException($"Custom event requires a valid priority value greater than {priorityMin}, value used: {value}");
				}
				_priority = value;
			}
		}

		public CustomEventWireModel(float priority, IDictionary<string, object> intrinsicAttributes, IDictionary<string, object> userAttributes)
		{
			Priority = priority;
			IntrinsicAttributes = intrinsicAttributes;
			UserAttributes = userAttributes;
		}
	}
}