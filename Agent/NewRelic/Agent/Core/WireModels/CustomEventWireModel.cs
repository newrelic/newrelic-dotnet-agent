using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{

	[JsonConverter(typeof(JsonArrayConverter))]
	public class CustomEventWireModel
	{
		[JsonArrayIndex(Index = 0)]
		[NotNull]
		public readonly IEnumerable<KeyValuePair<string, object>> IntrinsicAttributes;

		[JsonArrayIndex(Index = 1)]
		[NotNull]
		public readonly IEnumerable<KeyValuePair<string, object>> UserAttributes;

		private const string EventTypeKey = "type";

		private const string ItemTypeName = "Custom Event";
		private const string TimeStampKey = "timestamp";
		private const float PriorityMin = 0.0f;
		private static readonly string _missingTimestampMessage = $"{ItemTypeName} does not contain '{TimeStampKey}'";
		private float _priority;

		public float Priority
		{
			get { return _priority; }
			set
			{
				if (value < PriorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
				{
					throw new ArgumentException($"{ItemTypeName} requires a valid priority value greater than {PriorityMin}, value used: {value}");
				}
				_priority = value;
			}
		}

		//Compares on Priority (descending 1.0 -> 0.0), if priority is equal, compares timestamp values (ascending double values)
		//if priorities are equal the event that has the earliest timestamp will sort higher.
		public class PriorityTimestampComparer : Comparer<CustomEventWireModel>
		{
			public override int Compare(CustomEventWireModel x, CustomEventWireModel y)
			{
				//null cases: null < something, something > null, null == null
				if (x == null)
				{
					return (y == null) ? 0 : -1;
				}
				if (y == null)
				{
					return 1;
				}

				//descending by Priority, if equal resort to timestamp(ascending) to break the tie.
				//larger priority values are sorted to be first
				var priorityComparison = x.Priority.CompareTo(y.Priority);
				if (0 != priorityComparison)
				{
					//negate to achieve descending...
					return -priorityComparison;  
				}

				//priorities are equal, fetch the timestamps;
				if (!x.IntrinsicAttributes.ToDictionary().TryGetValue(TimeStampKey, out object xTimestampValue))
				{
					throw new ArgumentException(_missingTimestampMessage, nameof(x));
				}

				if (!y.IntrinsicAttributes.ToDictionary().TryGetValue(TimeStampKey, out object yTimestampValue))
				{
					throw new ArgumentException(_missingTimestampMessage, nameof(y));
				}

				var xTimestamp = (double)xTimestampValue;
				var yTimestamp = (double)yTimestampValue;
				return xTimestamp.CompareTo(yTimestamp);
			}
		}

		/// <param name="eventType">The event type.</param>
		/// <param name="eventTimeStamp">The start time of the event.</param>
		/// <param name="userAttributes">Additional attributes supplied by the user.</param>
		/// <param name="priority">value from 0.0-1.0 applied to items in ConcurrentPriorityQueue to increase the LIKELIHOOD that relate events are retained</param>
		public CustomEventWireModel([NotNull] String eventType, DateTime eventTimeStamp, [NotNull] IEnumerable<KeyValuePair<String, Object>> userAttributes, float priority)
		{
			Priority = priority;

			IntrinsicAttributes = new Dictionary<String, Object>
			{
				{EventTypeKey, eventType},
				{TimeStampKey, eventTimeStamp.ToUnixTime()},
			};

			UserAttributes = userAttributes.ToDictionary();
		}
	}
}