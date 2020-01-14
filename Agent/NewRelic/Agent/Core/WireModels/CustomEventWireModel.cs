using NewRelic.Agent.Core.JsonConverters;
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

		/// <param name="eventType">The event type.</param>
		/// <param name="eventTimeStamp">The start time of the event.</param>
		/// <param name="userAttributes">Additional attributes supplied by the user.</param>
		/// <param name="priority">value from 0.0-1.0 applied to items in ConcurrentPriorityQueue to increase the LIKELIHOOD that relate events are retained</param>
		public CustomEventWireModel(string eventType, DateTime eventTimeStamp, IEnumerable<KeyValuePair<string, object>> userAttributes, float priority)
		{
			const string eventTypeKey = "type";
			const string timeStampKey = "timestamp";
			Priority = priority;

			IntrinsicAttributes = new Dictionary<string, object>
			{
				{eventTypeKey, eventType},
				{timeStampKey, eventTimeStamp.ToUnixTimeMilliseconds()},
			};

			UserAttributes = userAttributes.ToDictionary();
		}
	}
}